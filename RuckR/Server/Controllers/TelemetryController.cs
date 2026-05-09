using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetryController : ControllerBase
    {
        private readonly ILogger<TelemetryController> _logger;

        private static readonly Regex GpsAcceptedRegex = new(
            @"ACCEPTED.*?(?:lat=|raw=\()(?<lat>-?\d+\.\d+).*?(?:lng=|,\s*)(?<lng>-?\d+\.\d+).*?accuracy=(?<acc>\d+)m(\s+displacement=(?<disp>\d+\.?\d*)m)?",
            RegexOptions.Compiled);

        private static readonly Regex GpsDiscardedRegex = new(
            @"discarding position.*displacement\s+(?<disp>\d+\.?\d*)m.*threshold\s+(?<thresh>\d+\.?\d*)m.*accuracy=(?<acc>\d+)m",
            RegexOptions.Compiled);

        public TelemetryController(ILogger<TelemetryController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public IActionResult Post([FromBody] ClientLogBatch batch)
        {
            if (batch.Entries.Count == 0)
            {
                return Ok();
            }

            var activity = Activity.Current;

            foreach (var entry in batch.Entries)
            {
                if (!Enum.TryParse<LogLevel>(entry.LogLevel, ignoreCase: true, out var level))
                {
                    level = LogLevel.Warning;
                }

                using var scope = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["ClientSessionId"] = batch.SessionId,
                    ["ClientCategory"] = entry.Category,
                    ["ClientTimestamp"] = entry.Timestamp,
                    ["ClientUrl"] = entry.Url,
                    ["ClientUserAgent"] = entry.UserAgent
                });

                _logger.Log(level, "Client {Level} [{Category}] {Message}{Exception}",
                    level,
                    entry.Category,
                    entry.Message,
                    entry.Exception != null ? $" | {entry.Exception}" : string.Empty);

                if (activity is not null
                    && entry.Category.Contains("GeolocationService", StringComparison.OrdinalIgnoreCase))
                {
                    var acceptedMatch = GpsAcceptedRegex.Match(entry.Message);
                    if (acceptedMatch.Success)
                    {
                        var tags = new ActivityTagsCollection
                        {
                            { "gps.lat", double.Parse(acceptedMatch.Groups["lat"].Value) },
                            { "gps.lng", double.Parse(acceptedMatch.Groups["lng"].Value) },
                            { "gps.accuracy_m", int.Parse(acceptedMatch.Groups["acc"].Value) },
                        };

                        var displacementGroup = acceptedMatch.Groups["disp"];
                        if (displacementGroup.Success)
                            tags.Add("gps.displacement_m", double.Parse(displacementGroup.Value));

                        activity.AddEvent(new ActivityEvent("gps.position_accepted", tags: tags));
                        continue;
                    }

                    var discardedMatch = GpsDiscardedRegex.Match(entry.Message);
                    if (discardedMatch.Success)
                    {
                        activity.AddEvent(new ActivityEvent("gps.position_discarded", tags: new ActivityTagsCollection
                        {
                            { "gps.displacement_m", double.Parse(discardedMatch.Groups["disp"].Value) },
                            { "gps.threshold_m", double.Parse(discardedMatch.Groups["thresh"].Value) },
                            { "gps.accuracy_m", int.Parse(discardedMatch.Groups["acc"].Value) },
                        }));
                    }
                }
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
