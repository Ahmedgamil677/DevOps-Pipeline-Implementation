using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests
{
    public abstract class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        protected readonly WebApplicationFactory<Program> Factory;
        protected readonly HttpClient Client;
        protected readonly JsonSerializerOptions JsonOptions;

        protected IntegrationTestBase(WebApplicationFactory<Program> factory)
        {
            Factory = factory;
            Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:5001"),
                HandleCookies = true
            });

            JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        protected async Task<T> DeserializeResponse<T>(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content, JsonOptions)!;
        }

        protected void AssertSuccessStatusCode(HttpResponseMessage response)
        {
            response.IsSuccessStatusCode.Should().BeTrue(
                $"Expected successful status but got {response.StatusCode}. Content: {response.Content.ReadAsStringAsync().Result}");
        }

        public virtual void Dispose()
        {
            Client?.Dispose();
            Factory?.Dispose();
        }
    }
}