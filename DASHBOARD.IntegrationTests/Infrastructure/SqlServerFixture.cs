using DASHBOARD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace DASHBOARD.IntegrationTests.Infrastructure;

/// <summary>
/// Starts one SQL Server container for the whole integration-test run and applies the
/// application's migrations to it.
/// </summary>
/// <remarks>
/// A real relational database is the point of this test project. The unit-test project runs against
/// an in-memory provider, which cannot honour the two things production depends on: column types
/// declared per provider (<c>nvarchar(max)</c>) and <c>HasDefaultValue</c> columns that the
/// <c>HasData</c> seed relies on. Both are exercised here, by the migrations themselves.
///
/// The container is expensive (image pull plus migration), so it is shared across every test class
/// in <see cref="IntegrationTestCollection"/> and each test isolates itself by data rather than by
/// database — see <c>ApiFactory</c>.
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    /// <summary>Gets the connection string of the running container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Starts the container and brings the schema up to the latest migration.</summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Migrate rather than EnsureCreated: this is the only place the migration chain is proven
        // to run end to end, seed data included.
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>Stops and removes the container.</summary>
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates a context pointed at the container, for direct arrange/assert in tests.</summary>
    /// <returns>A new <see cref="ApplicationDbContext"/> the caller owns and must dispose.</returns>
    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}

/// <summary>Shares one <see cref="SqlServerFixture"/> across every integration-test class.</summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<SqlServerFixture>
{
    /// <summary>The collection name test classes reference.</summary>
    public const string Name = "integration";
}
