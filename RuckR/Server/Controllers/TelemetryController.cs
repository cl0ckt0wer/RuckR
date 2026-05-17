using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    /// <summary>API for ingesting and summarizing client telemetry logs.</summary>
    [ApiController]
    [Route("api/[controller]")]
    /// <summary>Defines the server-side class TelemetryController.</summary>
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
    /// <summary>Initializes a new instance of <see cref="TelemetryController"/>.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="db">The database context.</param>
    public TelemetryController(ILogger<TelemetryController> logger, RuckRDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        /// <summary>Persist a telemetry batch from the client.</summary>
        /// <param name="batch">The batch.</param>
        /// <returns>The operation result.</returns>
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

                if (activity is not null
                    && entry.Category.Contains("Browser", StringComparison.OrdinalIgnoreCase))
                {
                    activity.AddEvent(new ActivityEvent("browser.console_error", tags: new ActivityTagsCollection
                    {
                        { "browser.message", TruncateTag(entry.Message, 1000) },
                        { "browser.exception", TruncateTag(entry.Exception, 2000) },
                        { "browser.url", entry.Url },
                        { "browser.user_agent", entry.UserAgent },
                        { "browser.level", level.ToString() }
                    }));
                }
            }
            return Ok();
        }

        private static string? TruncateTag(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
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
                AddStringTag(tags, "map.page.url", root, "url");
                AddStringTag(tags, "map.page.path", root, "route", "path");
                AddStringTag(tags, "map.page.search", root, "route", "search");
                AddStringTag(tags, "map.debug.mode", root, "route", "debugMode");
                AddStringTag(tags, "map.flag.basemap", root, "route", "basemap");
                AddStringTag(tags, "map.flag.map_graphics", root, "route", "mapGraphics");
                AddStringTag(tags, "map.flag.auto_gps", root, "route", "autoGps");
                AddStringTag(tags, "map.flag.map_diagnostics", root, "route", "mapDiagnostics");
                AddStringTag(tags, "map.flag.arcgis_widgets", root, "route", "arcGisWidgets");
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
                AddBooleanTag(tags, "map.visual.canvas_present", root, "visual", "canvas", "present");
                AddStringTag(tags, "map.visual.canvas_method", root, "visual", "canvas", "method");
                AddStringTag(tags, "map.visual.canvas_error", root, "visual", "canvas", "error");
                AddNumberTag(tags, "map.visual.canvas_samples", root, "visual", "canvas", "samples");
                AddNumberTag(tags, "map.visual.canvas_transparent_ratio", root, "visual", "canvas", "transparentRatio");
                AddNumberTag(tags, "map.visual.canvas_white_ratio", root, "visual", "canvas", "whiteRatio");
                AddNumberTag(tags, "map.visual.canvas_dark_ratio", root, "visual", "canvas", "darkRatio");
                AddNumberTag(tags, "map.visual.canvas_varied_ratio", root, "visual", "canvas", "variedRatio");
                AddNumberTag(tags, "map.visual.canvas_center_r", root, "visual", "canvas", "center", "r");
                AddNumberTag(tags, "map.visual.canvas_center_g", root, "visual", "canvas", "center", "g");
                AddNumberTag(tags, "map.visual.canvas_center_b", root, "visual", "canvas", "center", "b");
                AddNumberTag(tags, "map.visual.canvas_center_a", root, "visual", "canvas", "center", "a");
                AddStringTag(tags, "map.visual.top_element_tag", root, 0, "visual", "elementStackAtCenter", "tag");
                AddStringTag(tags, "map.visual.top_element_class", root, 0, "visual", "elementStackAtCenter", "className");
                AddStringTag(tags, "map.visual.top_element_testid", root, 0, "visual", "elementStackAtCenter", "testId");
                AddBooleanTag(tags, "map.arcgis.present", root, "arcgis", "present");
                AddBooleanTag(tags, "map.arcgis.ready", root, "arcgis", "ready");
                AddBooleanTag(tags, "map.arcgis.updating", root, "arcgis", "updating");
                AddBooleanTag(tags, "map.arcgis.stationary", root, "arcgis", "stationary");
                AddBooleanTag(tags, "map.arcgis.suspended", root, "arcgis", "suspended");
                AddNumberTag(tags, "map.arcgis.view_width", root, "arcgis", "width");
                AddNumberTag(tags, "map.arcgis.view_height", root, "arcgis", "height");
                AddNumberTag(tags, "map.arcgis.zoom", root, "arcgis", "zoom");
                AddNumberTag(tags, "map.arcgis.scale", root, "arcgis", "scale");
                AddNumberTag(tags, "map.arcgis.center_lat", root, "arcgis", "center", "latitude");
                AddNumberTag(tags, "map.arcgis.center_lng", root, "arcgis", "center", "longitude");
                AddStringTag(tags, "map.arcgis.map_load_status", root, "arcgis", "map", "loadStatus");
                AddStringTag(tags, "map.arcgis.basemap_id", root, "arcgis", "map", "basemapId");
                AddStringTag(tags, "map.arcgis.basemap_title", root, "arcgis", "map", "basemapTitle");
                AddBooleanTag(tags, "map.arcgis.map_loaded", root, "arcgis", "map", "loaded");
                AddBooleanTag(tags, "map.arcgis.basemap_loaded", root, "arcgis", "map", "basemapLoaded");
                AddNumberTag(tags, "map.arcgis.layers", root, "arcgis", "map", "layers");
                AddNumberTag(tags, "map.arcgis.all_layers", root, "arcgis", "map", "allLayers");
                AddNumberTag(tags, "map.arcgis.layer_views", root, "arcgis", "layerViews");
                AddNumberTag(tags, "map.console.recent_count", root, "console", "recent");
                AddNumberTag(tags, "map.console.error_count", root, "console", "recent", "error");
                AddNumberTag(tags, "map.console.warn_count", root, "console", "recent", "warn");

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

        private static void AddStringTag(ActivityTagsCollection tags, string tagName, JsonElement root, int arrayIndex, params string[] path)
        {
            if (TryGetArrayItem(root, out var item, arrayIndex, path) && item.ValueKind == JsonValueKind.String)
            {
                tags.Add(tagName, item.GetString());
            }
            else if (path.Length > 0
                && TryGetArrayItem(root, out item, arrayIndex, path[..^1])
                && item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty(path[^1], out var property)
                && property.ValueKind == JsonValueKind.String)
            {
                tags.Add(tagName, property.GetString());
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
            else if (TryGetElement(root, out element, path) && element.ValueKind == JsonValueKind.Array)
            {
                tags.Add(tagName, element.GetArrayLength());
            }
            else if (path.Length > 0
                && TryGetElement(root, out element, path[..^1])
                && element.ValueKind == JsonValueKind.Array)
            {
                var count = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object
                        && item.TryGetProperty("level", out var level)
                        && level.ValueKind == JsonValueKind.String
                        && string.Equals(level.GetString(), path[^1], StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                    }
                }

                tags.Add(tagName, count);
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

        private static bool TryGetArrayItem(JsonElement root, out JsonElement item, int arrayIndex, params string[] path)
        {
            item = default;
            if (!TryGetElement(root, out var array, path) || array.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            if (arrayIndex < 0 || arrayIndex >= array.GetArrayLength())
            {
                return false;
            }

            var currentIndex = 0;
            foreach (var currentItem in array.EnumerateArray())
            {
                if (currentIndex == arrayIndex)
                {
                    item = currentItem;
                    return true;
                }

                currentIndex++;
            }

            return false;
        }

        /// <summary>Check database health and app runtime status.</summary>
        /// <returns>The operation result.</returns>
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

        /// <summary>Get service and telemetry status details.</summary>
        /// <returns>The operation result.</returns>
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

