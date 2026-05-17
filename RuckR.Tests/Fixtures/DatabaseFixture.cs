using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using Testcontainers.MsSql;

namespace RuckR.Tests.Fixtures;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _dbContainer;
    private DbContextOptions<RuckRDbContext>? _options;

    /// <summary>
    /// Verifies db Container.Get Connection String.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public string ConnectionString => _dbContainer.GetConnectionString();

    /// <summary>
    /// Initializes a new instance of the <see cref="""DatabaseFixture"""/> class.
    /// </summary>
    public DatabaseFixture()
    {
        _dbContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword(TestSqlPassword.Create())
            .Build();
    }

    /// <summary>
    /// Verifies initialize Async.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        _options = new DbContextOptionsBuilder<RuckRDbContext>()
            .UseSqlServer(ConnectionString, x => x.UseNetTopologySuite())
            .Options;

        await using var db = new RuckRDbContext(_options);
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Verifies create Db Context.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public RuckRDbContext CreateDbContext()
    {
        if (_options == null)
            throw new InvalidOperationException("DatabaseFixture not initialized");
        return new RuckRDbContext(_options);
    }

    /// <summary>
    /// Verifies dispose Async.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_dbContainer != null)
            await _dbContainer.DisposeAsync();
    }
}


