using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<SampleHealthCheck>("sample_health_check");

// Configure logging
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
    loggingBuilder.AddApplicationInsights();
});

// Add Application Insights if configured
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
    });
    
    // Development middleware
    app.UseDeveloperExceptionPage();
}

if (app.Environment.IsProduction())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Add security headers in production
if (app.Environment.IsProduction())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        await next();
    });
}

app.UseAuthorization();

// Custom middleware for request logging
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Handling request: {Method} {Path}", context.Request.Method, context.Request.Path);
    await next();
    logger.LogInformation("Response: {StatusCode} for {Method} {Path}", 
        context.Response.StatusCode, context.Request.Method, context.Request.Path);
});

app.MapControllers();
app.MapHealthChecks("/health");

// Global error handling endpoint
app.Map("/error", async (HttpContext context) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError("An unhandled exception occurred for request: {Path}", context.Request.Path);
    
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new
    {
        error = "An unexpected error occurred",
        requestId = context.TraceIdentifier,
        timestamp = DateTime.UtcNow
    });
});

// Root endpoint
app.MapGet("/", () => 
{
    return Results.Json(new 
    { 
        message = "Welcome to Azure DevOps Pipeline Demo API",
        version = "1.0.0",
        environment = app.Environment.EnvironmentName,
        timestamp = DateTime.UtcNow
    });
});

// API info endpoint
app.MapGet("/api/info", () =>
{
    return Results.Json(new
    {
        application = "Azure DevOps Pipeline Demo",
        version = "1.0.0",
        environment = app.Environment.EnvironmentName,
        server = Environment.MachineName,
        framework = ".NET 6.0",
        timestamp = DateTime.UtcNow
    });
});

try
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Starting application in {Environment} environment", app.Environment.EnvironmentName);
    
    await app.RunAsync();
    
    logger.LogInformation("Application stopped gracefully");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Application terminated unexpectedly in {Environment} environment", app.Environment.EnvironmentName);
}
finally
{
    // Ensure logs are flushed
    await app.DisposeAsync();
}