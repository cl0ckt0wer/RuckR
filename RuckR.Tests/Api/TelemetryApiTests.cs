using System.Net;
using System.Net.Http.Json;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.Api;

[Collection(nameof(TestCollection))]
public class TelemetryApiTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public TelemetryApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Telemetry endpoint is unauthenticated — use plain client
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
    }

    [Fact]
    public async Task PostTelemetry_ValidBatch_Returns200()
    {
        var batch = new ClientLogBatch
        {
            SessionId = "test-abc123",
            Entries = new List<ClientLogEntry>
            {
                new()
                {
                    Timestamp = DateTime.UtcNow,
                    LogLevel = "Warning",
                    Category = "Test.Component",
                    Message = "Something happened",
                    Exception = null
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/telemetry", batch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostTelemetry_EmptyBatch_Returns200()
    {
        var batch = new ClientLogBatch
        {
            SessionId = "empty-test",
            Entries = new List<ClientLogEntry>()
        };

        var response = await _client.PostAsJsonAsync("/api/telemetry", batch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostTelemetry_MultipleEntries_Returns200()
    {
        var batch = new ClientLogBatch
        {
            SessionId = "multi-test",
            Entries = new List<ClientLogEntry>
            {
                new() { Timestamp = DateTime.UtcNow, LogLevel = "Information", Category = "C1", Message = "M1" },
                new() { Timestamp = DateTime.UtcNow, LogLevel = "Warning", Category = "C2", Message = "M2", Exception = "ex" },
                new() { Timestamp = DateTime.UtcNow, LogLevel = "Error", Category = "C3", Message = "M3" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/telemetry", batch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
