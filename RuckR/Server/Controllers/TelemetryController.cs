using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetryController : ControllerBase
    {
        private readonly ILogger<TelemetryController> _logger;
        private readonly RuckRDbContext _db;

        private static readonly Regex GpsAcceptedRegex = new(
            @"ACCEPTED.*?(?:lat=|raw=\()(?<lat>-?\d+\.\d+).*?(?:lng=|,\s*)(?<lng>-?\d+\.\d+).*?accuracy=(?<acc>\d+)m(\s+displacement=(?<disp>\d+\.?\d*)m)?",
            RegexOptions.Compiled);

        private static readonly Regex GpsDiscardedRegex = new(
            @"discarding position.*displacement\s+(?<disp>\d+\.?\d*)m.*threshold\s+(?<thresh>\d+\.?\d*)m.*accuracy=(?<acc>\d+)m",
            RegexOptions.Compiled);

        public TelemetryController(ILogger<TelemetryController> logger, RuckRDbContext db)
        {
            _logger = logger;
            _db = db;
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

                if (activity is not null
                    && entry.Category.Contains("Map", StringComparison.OrdinalIgnoreCase)
                    && entry.Message.StartsWith("MapDiagnostics ", StringComparison.OrdinalIgnoreCase))
                {
                    AddMapDiagnosticsEvent(activity, entry.Message);
                }
            }
            return Ok();
        }

        private static void AddMapDiagnosticsEvent(Activity activity, string message)
        {
            var jsonStart = message.IndexOf('{');
            if (jsonStart < 0)
            {
                activity.AddEvent(new ActivityEvent("map.diagnostics"));
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(message[jsonStart..]);
                var root = doc.RootElement;
                var tags = new ActivityTagsCollection();

                AddStringTag(tags, "map.diagnostics.reason", root, "reason");
                AddNumberTag(tags, "map.viewport.width", root, "viewport", "width");
                AddNumberTag(tags, "map.viewport.height", root, "viewport", "height");
                AddNumberTag(tags, "map.viewport.dpr", root, "viewport", "dpr");
                AddNumberTag(tags, "map.container.height", root, "mapContainer", "rect", "height");
                AddNumberTag(tags, "map.wrapper.height", root, "mapWrapper", "rect", "height");
                AddNumberTag(tags, "map.arcgis.height", root, "arcgisMap", "rect", "height");
                AddNumberTag(tags, "map.esri.height", root, "esriView", "rect", "height");
                AddNumberTag(tags, "map.controls.visible", root, "controls", "visible");
                AddNumberTag(tags, "map.controls.count", root, "controls", "count");
                AddBooleanTag(tags, "map.css.app_found", root, "css", "app", "found");
                AddBooleanTag(tags, "map.css.wrapper_rule", root, "css", "app", "mapWrapperRule");
                AddBooleanTag(tags, "map.webgl.context", root, "webGl", "context");
                AddNumberTag(tags, "map.webgl.buffer_height", root, "webGl", "drawingBufferHeight");
                AddNumberTag(tags, "map.resources.arcgis_geoblazor", root, "resources", "arcGisOrGeoBlazor");

                activity.AddEvent(new ActivityEvent("map.diagnostics", tags: tags));
            }
            catch (JsonException)
            {
                activity.AddEvent(new ActivityEvent("map.diagnostics_parse_failed"));
            }
        }

        private static void AddStringTag(ActivityTagsCollection tags, string tagName, JsonElement root, params string[] path)
        {
            if (TryGetElement(root, out var element, path) && element.ValueKind == JsonValueKind.String)
            {
                tags.Add(tagName, element.GetString());
            }
        }

        private static void AddBooleanTag(ActivityTagsCollection tags, string tagName, JsonElement root, params string[] path)
        {
            if (TryGetElement(root, out var element, path)
                && element.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                tags.Add(tagName, element.GetBoolean());
            }
        }

        private static void AddNumberTag(ActivityTagsCollection tags, string tagName, JsonElement root, params string[] path)
        {
            if (TryGetElement(root, out var element, path) && element.ValueKind == JsonValueKind.Number)
            {
                tags.Add(tagName, element.GetDouble());
            }
        }

        private static bool TryGetElement(JsonElement root, out JsonElement element, params string[] path)
        {
            element = root;
            foreach (var segment in path)
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element))
                {
                    return false;
                }
            }

            return true;
        }

        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            // Verify DB connectivity with a lightweight query
            bool dbHealthy = false;
            try
            {
                dbHealthy = await _db.Database.CanConnectAsync();
            }
            catch
            {
                dbHealthy = false;
            }

            if (!dbHealthy)
            {
                return StatusCode(503, new { status = "unhealthy", reason = "database unavailable", timestamp = DateTime.UtcNow });
            }

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
