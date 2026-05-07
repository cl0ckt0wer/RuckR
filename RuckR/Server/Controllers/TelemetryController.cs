using Microsoft.AspNetCore.Mvc;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetryController : ControllerBase
    {
        private readonly ILogger<TelemetryController> _logger;

        public TelemetryController(ILogger<TelemetryController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public IActionResult Post([FromBody] ClientLogBatch batch)
        {
            foreach (var entry in batch.Entries)
            {
                var level = Enum.Parse<LogLevel>(entry.LogLevel);
                _logger.Log(level, "[Client:{Category}] {Message}{Exception}",
                    entry.Category, entry.Message,
                    entry.Exception != null ? $" | {entry.Exception}" : "");
            }
            return Ok();
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }

        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            // Basic OTEL status — what's running
            var result = new
            {
                serviceName = "RuckR.Server",
                exporters = new[] { "Console", "OTLP" },
                otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "not set (default http://localhost:4317)",
                signals = new[] { "traces", "metrics", "logs" },
                httpLoggingEnabled = true,
                uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            };
            return Ok(result);
        }
    }
}
