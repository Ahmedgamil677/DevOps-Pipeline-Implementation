using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebApp
{
    public class SampleHealthCheck : IHealthCheck
    {
        private readonly ILogger<SampleHealthCheck> _logger;

        public SampleHealthCheck(ILogger<SampleHealthCheck> logger)
        {
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Simulate some health check logic
                var memoryUsage = GC.GetTotalMemory(false);
                var maxMemory = 100 * 1024 * 1024; // 100MB threshold

                if (memoryUsage > maxMemory)
                {
                    _logger.LogWarning("High memory usage detected: {MemoryUsage} bytes", memoryUsage);
                    return Task.FromResult(
                        HealthCheckResult.Degraded($"High memory usage: {memoryUsage} bytes"));
                }

                // Check if we can write to disk (simplified)
                var tempPath = Path.GetTempPath();
                var testFile = Path.Combine(tempPath, $"healthcheck-{Guid.NewGuid()}.tmp");
                File.WriteAllText(testFile, "healthcheck");
                File.Delete(testFile);

                _logger.LogInformation("Health check passed successfully");
                return Task.FromResult(
                    HealthCheckResult.Healthy("Application is healthy"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return Task.FromResult(
                    HealthCheckResult.Unhealthy("Health check failed", ex));
            }
        }
    }
}