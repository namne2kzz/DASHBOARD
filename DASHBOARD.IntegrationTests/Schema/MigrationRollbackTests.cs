using DASHBOARD.IntegrationTests.Infrastructure;
using DASHBOARD.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DASHBOARD.IntegrationTests.Schema;

/// <summary>
/// Builds a throwaway database from scratch, then migrates it down and back up again.
/// </summary>
/// <remarks>
/// <c>MigrationTests</c> proves the chain runs forward on an empty database, which is what CI and a
/// fresh environment need. This file covers the other direction, which is what a rollback needs:
/// every migration's <c>Down()</c> must undo its <c>Up()</c> well enough that the chain can be
/// re-applied. A <c>Down()</c> that drops a table without recreating its indexes, or that forgets a
/// column, only shows itself on the way back up — and by then it is being discovered during an
/// incident, on production, under time pressure.
///
/// This is also the one place a missing or empty migration body is visible. Two such migrations
/// were already found in this project: four files dropped from the repository during a folder move,
/// and an <c>AddOrganizations</c> whose <c>Up()</c> was empty, which together meant a fresh database
/// had no <c>Organizations</c> table at all while the developer machines that had already run the
/// original migration looked fine.
///
/// Everything here runs against its own database rather than the shared fixture one, because
/// migrating down would otherwise tear the schema out from under every other test class in the
/// collection. The database is dropped in <see cref="DisposeAsync"/> even when the test fails.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
public sealed class MigrationRollbackTests(SqlServerFixture database) : IAsyncLifetime
{
    /// <summary>A database name unique to this run, so a leftover copy never collides.</summary>
    private readonly string _databaseName = $"DASHBOARD_Rollback_{Guid.NewGuid():N}"[..40];

    private string _connectionString = string.Empty;

    /// <summary>Points a connection string at a new database on the fixture's server.</summary>
    public Task InitializeAsync()
    {
        _connectionString = new SqlConnectionStringBuilder(database.ConnectionString)
        {
            InitialCatalog = _databaseName,
        }.ConnectionString;

        return Task.CompletedTask;
    }

    /// <summary>Drops the throwaway database.</summary>
    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    /// <summary>All migrations in the order EF applies them.</summary>
    private static IReadOnlyList<string> AllMigrations(ApplicationDbContext context) =>
        [.. context.Database.GetService<IMigrationsAssembly>().Migrations.Keys];

    // ── Down, then up again ─────────────────────────────────────────────────

    [Fact]
    public async Task TheChainSurvivesAFullDownAndUpCycle()
    {
        await using var context = CreateContext();
        var migrator = context.Database.GetService<IMigrator>();

        // Forward to the latest, on a database that did not exist a moment ago.
        await migrator.MigrateAsync();

        var applied = await context.Database.GetAppliedMigrationsAsync();
        applied.Should().HaveCount(AllMigrations(context).Count);

        // "0" is EF's name for the empty database — every Down() in reverse order.
        await migrator.MigrateAsync("0");

        (await context.Database.GetAppliedMigrationsAsync())
            .Should().BeEmpty("migrating to 0 unwinds the whole chain");

        // And forward once more. This is the assertion that matters: a Down() that left the
        // schema subtly wrong makes the second Up() fail where the first one passed.
        await migrator.MigrateAsync();

        (await context.Database.GetPendingMigrationsAsync())
            .Should().BeEmpty("the chain must be re-appliable after a rollback");
    }

    [Fact]
    public async Task RollingBackOneMigrationAndReapplyingItWorks()
    {
        // The realistic incident shape: a release goes out, something is wrong, and only the last
        // migration is reverted. A full down-and-up would hide a Down() that is broken only when
        // the rest of the schema is still in place.
        await using var context = CreateContext();
        var migrator = context.Database.GetService<IMigrator>();

        await migrator.MigrateAsync();

        var migrations = AllMigrations(context);
        var previous   = migrations[^2];
        var last       = migrations[^1];

        await migrator.MigrateAsync(previous);

        (await context.Database.GetAppliedMigrationsAsync())
            .Should().NotContain(last);

        await migrator.MigrateAsync(last);

        (await context.Database.GetAppliedMigrationsAsync())
            .Should().Contain(last);
    }

    [Fact]
    public async Task AfterAFullCycleTheSchemaStillMatchesTheModel()
    {
        // Re-applying without error is not enough on its own: the tables could come back subtly
        // different. Every mapped table is selected from, which names each mapped column.
        await using var context = CreateContext();
        var migrator = context.Database.GetService<IMigrator>();

        await migrator.MigrateAsync();
        await migrator.MigrateAsync("0");
        await migrator.MigrateAsync();

        var failures = new List<string>();

        foreach (var entityType in context.Model.GetEntityTypes()
                     .Where(t => t.ClrType is not null && !t.IsOwned()))
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    $"SELECT TOP 1 * FROM [{entityType.GetTableName()}]");
            }
            catch (Exception ex)
            {
                failures.Add($"{entityType.ClrType.Name}: {ex.Message}");
            }
        }

        failures.Should().BeEmpty(
            "a rebuilt schema must still carry every table and column the model maps");
    }

    [Fact]
    public async Task NoMigrationHasAnEmptyUpBody()
    {
        // The AddOrganizations file in this project was committed with an empty Up(), so a fresh
        // database silently came out without the Organizations table while every machine that had
        // already run the original migration looked healthy. An empty Up() is occasionally
        // legitimate, but never silently — this fails so the author has to say so deliberately.
        await using var context = CreateContext();

        var assembly = context.Database.GetService<IMigrationsAssembly>();
        var empty    = new List<string>();

        foreach (var (id, typeInfo) in assembly.Migrations)
        {
            var migration  = (Migration)Activator.CreateInstance(typeInfo.AsType())!;
            var operations = migration.UpOperations;

            if (operations.Count == 0)
                empty.Add(id);
        }

        empty.Should().BeEmpty(
            "a migration whose Up() does nothing leaves a fresh database missing whatever it " +
            "was supposed to create");
    }
}
