using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using Testcontainers.MsSql;

namespace RuckR.Tests.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _dbContainer;
    private DbContextOptions<RuckRDbContext>? _options;

    public string ConnectionString => _dbContainer.GetConnectionString();

    public DatabaseFixture()
    {
        _dbContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword(TestSqlPassword.Create())
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        _options = new DbContextOptionsBuilder<RuckRDbContext>()
            .UseSqlServer(ConnectionString, x => x.UseNetTopologySuite())
            .Options;

        await using var db = new RuckRDbContext(_options);
        await db.Database.MigrateAsync();
    }

    public RuckRDbContext CreateDbContext()
    {
        if (_options == null)
            throw new InvalidOperationException("DatabaseFixture not initialized");
        return new RuckRDbContext(_options);
    }

    public async Task DisposeAsync()
    {
        if (_dbContainer != null)
            await _dbContainer.DisposeAsync();
    }
}
