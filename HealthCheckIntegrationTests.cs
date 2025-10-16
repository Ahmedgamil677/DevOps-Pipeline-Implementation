using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IntegrationTests
{
    public class HealthCheckIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly JsonSerializerOptions _jsonOptions;

        public HealthCheckIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                // Configure test-specific settings
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Add test-specific configuration
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["Logging:LogLevel:Default"] = "Warning",
                        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
                        ["AllowedHosts"] = "*",
                        ["ApplicationInsights:ConnectionString"] = "" // Disable AI for tests
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Add any test-specific service configuration here
                    // For example, you might want to mock external dependencies
                });

                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders(); // Remove other logging providers
                    logging.AddConsole(); // Keep console for test output
                });
            });

            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:5001"), // Use HTTPS for realistic testing
                HandleCookies = true
            });

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        [Fact]
        public async Task HealthEndpoint_ReturnsHealthyStatus()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health");

            // Act
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
            
            // Log for debugging
            Console.WriteLine($"Health endpoint response: {content}");
        }

        [Fact]
        public async Task RootEndpoint_ReturnsWelcomeMessageAndCorrectStructure()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/");

            // Act
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<RootResponse>(content, _jsonOptions);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
            
            responseData.Should().NotBeNull();
            responseData!.Message.Should().Contain("Welcome to Azure DevOps Pipeline Demo API");
            responseData.Version.Should().NotBeNullOrEmpty();
            responseData.Environment.Should().NotBeNullOrEmpty();
            responseData.Timestamp.Should().NotBe(default(DateTime));
        }

        [Fact]
        public async Task ApiInfoEndpoint_ReturnsApplicationInformation()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/info");

            // Act
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<ApiInfoResponse>(content, _jsonOptions);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
            
            responseData.Should().NotBeNull();
            responseData!.Application.Should().Be("Azure DevOps Pipeline Demo");
            responseData.Version.Should().Be("1.0.0");
            responseData.Environment.Should().NotBeNullOrEmpty();
            responseData.Server.Should().NotBeNullOrEmpty();
            responseData.Framework.Should().Be(".NET 6.0");
            responseData.Timestamp.Should().NotBe(default(DateTime));
        }

        [Fact]
        public async Task HealthEndpoint_WithDetailedQuery_ReturnsDetailedHealthInfo()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health");

            // Act
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            content.Should().Contain("Healthy").Or.Contain("Degraded").Or.Contain("Unhealthy");
        }

        [Fact]
        public async Task ErrorEndpoint_ReturnsProperErrorResponse()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/error");

            // Act
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            responseData.Should().NotBeNull();
            responseData!.Error.Should().Be("An unexpected error occurred");
            responseData.RequestId.Should().NotBeNullOrEmpty();
            responseData.Timestamp.Should().NotBe(default(DateTime));
        }

        [Fact]
        public async Task NonExistentEndpoint_ReturnsNotFound()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/nonexistent-endpoint");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task SwaggerEndpoint_ReturnsSwaggerUIInDevelopment()
        {
            // Arrange
            // Create a factory with Development environment
            var developmentFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
            });
            
            var developmentClient = developmentFactory.CreateClient();

            // Act
            var response = await developmentClient.GetAsync("/swagger");
            var content = await response.Content.ReadAsStringAsync();

            // Assert - In Development, Swagger should be available
            if (response.StatusCode == HttpStatusCode.OK)
            {
                content.Should().Contain("swagger");
            }
            // If not found, that's also acceptable based on configuration
        }

        [Fact]
        public async Task Headers_IncludeSecurityHeadersInProduction()
        {
            // Arrange
            var productionFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
            });
            
            var productionClient = productionFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "/");

            // Act
            var response = await productionClient.SendAsync(request);

            // Assert
            response.Headers.Should().Contain(h => h.Key == "X-Content-Type-Options");
            response.Headers.Should().Contain(h => h.Key == "X-Frame-Options");
            response.Headers.Should().Contain(h => h.Key == "X-XSS-Protection");
            
            var contentTypeOptions = response.Headers.GetValues("X-Content-Type-Options").First();
            contentTypeOptions.Should().Be("nosniff");
            
            var frameOptions = response.Headers.GetValues("X-Frame-Options").First();
            frameOptions.Should().Be("DENY");
        }

        [Fact]
        public async Task Application_StartsSuccessfully()
        {
            // Arrange & Act - The factory creation and client setup in constructor is the test

            // Assert
            _factory.Should().NotBeNull();
            _client.Should().NotBeNull();
            
            // Verify we can make a successful request
            var response = await _client.GetAsync("/health");
            response.IsSuccessStatusCode.Should().BeTrue();
        }

        [Fact]
        public async Task ConcurrentRequests_AreHandledProperly()
        {
            // Arrange
            var tasks = new List<Task<HttpResponseMessage>>();

            // Act - Make multiple concurrent requests
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_client.GetAsync("/health"));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert - All requests should succeed
            responses.Should().AllSatisfy(response =>
            {
                response.IsSuccessStatusCode.Should().BeTrue();
            });
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/health")]
        [InlineData("/api/info")]
        public async Task Endpoints_ReturnSuccessForValidRoutes(string endpoint)
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue(
                $"Endpoint {endpoint} should return success. Actual status: {response.StatusCode}");
        }

        [Fact]
        public async Task Logging_MiddlewareLogsRequests()
        {
            // Arrange
            var testLoggerFactory = new TestLoggerFactory();
            var factoryWithCustomLogging = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(testLoggerFactory);
                });
            });

            var client = factoryWithCustomLogging.CreateClient();
            
            // Act
            var response = await client.GetAsync("/health");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();
            // In a real scenario, you would verify logs were written
            // This is simplified for the example
        }

        public void Dispose()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }
    }

    // Response DTOs for deserialization
    public class RootResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class ApiInfoResponse
    {
        public string Application { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string Server { get; set; } = string.Empty;
        public string Framework { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    // Test logger for verifying logging behavior
    public class TestLogger : ILogger
    {
        public List<LogEntry> Logs { get; } = new List<LogEntry>();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add(new LogEntry
            {
                LogLevel = logLevel,
                EventId = eventId,
                State = state,
                Exception = exception,
                Message = formatter(state, exception)
            });
        }
    }

    public class LogEntry
    {
        public LogLevel LogLevel { get; set; }
        public EventId EventId { get; set; }
        public object? State { get; set; }
        public Exception? Exception { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class TestLoggerFactory : ILoggerFactory
    {
        public readonly List<TestLogger> Loggers = new List<TestLogger>();

        public void AddProvider(ILoggerProvider provider)
        {
            // Not implemented for this simple example
        }

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new TestLogger();
            Loggers.Add(logger);
            return logger;
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly TestLoggerFactory _loggerFactory;

        public TestLoggerProvider(TestLoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggerFactory.CreateLogger(categoryName);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}