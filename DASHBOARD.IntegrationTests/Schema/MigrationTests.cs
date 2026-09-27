using DASHBOARD.Domain.Entities;
using DASHBOARD.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.IntegrationTests.Schema;

/// <summary>
/// Proves the migration chain and the seed data actually apply to SQL Server.
/// </summary>
/// <remarks>
/// The unit-test project cannot check any of this. Its in-memory provider has no concept of a
/// column default, which is how <c>Repository.IsArchived</c> is filled for the seeded row, and it
/// ignores provider-specific column types such as <c>nvarchar(max)</c>. Both only hold up against a
/// real database — and the fixture has already run <c>Database.MigrateAsync()</c> to get here, so
/// simply reaching these assertions is part of the test.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
public sealed class MigrationTests(SqlServerFixture database)
{
    [Fact]
    public async Task EveryMigrationHasBeenApplied()
    {
        await using var db = database.CreateContext();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        var pending = await db.Database.GetPendingMigrationsAsync();

        applied.Should().NotBeEmpty("the fixture migrates the database before any test runs");
        pending.Should().BeEmpty("a migration that never runs in CI is a migration nobody has tested");
    }

    [Fact]
    public async Task EveryEntityTypeCanBeQueriedAgainstTheRealSchema()
    {
        await using var db = database.CreateContext();

        // A model that drifted from the last migration shows up here: the LINQ translates, but the
        // SQL names a column the database does not have. Unit tests never catch this, because the
        // in-memory provider builds its tables from the current model rather than from migrations.
        var failures = new List<string>();

        foreach (var entityType in db.Model.GetEntityTypes()
                     .Where(t => t.ClrType is not null && !t.IsOwned()))
        {
            try
            {
                // Materialise one row so every mapped column appears in the SELECT list.
                await db.Database.ExecuteSqlRawAsync(
                    $"SELECT TOP 1 * FROM [{entityType.GetTableName()}]");
            }
            catch (Exception ex)
            {
                failures.Add($"{entityType.ClrType.Name}: {ex.Message}");
            }
        }

        failures.Should().BeEmpty(
            "every mapped table must exist in the migrated schema");
    }

    [Fact]
    public async Task TheSeededRepositoryRowExists()
    {
        await using var db = database.CreateContext();

        // SeedData declares this row through an anonymous object that omits IsArchived, relying on
        // the column default. That is exactly what the in-memory provider could not reproduce.
        var seeded = await db.Repositories
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Code == "DASH");

        seeded.Should().NotBeNull("the HasData seed must survive a real migration");
        seeded!.IsArchived.Should().BeFalse("the column default supplies the value the seed omits");
    }

    [Fact]
    public async Task TheSeededRolesCarryTheirPermissionSets()
    {
        await using var db = database.CreateContext();

        // AllowedFunctions is stored as JSON in an nvarchar(max) column — a shape SQLite rejects
        // outright, so this round-trip is only provable here.
        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.IsDefault)
            .ToListAsync();

        roles.Should().NotBeEmpty();
        roles.Should().Contain(r => r.AllowedFunctions.Count > 0,
            "the JSON permission list must deserialise back out of the database");
    }

    [Fact]
    public async Task LongTextRoundTripsThroughAnNvarcharMaxColumn()
    {
        await using var db = database.CreateContext();

        var repositoryId = (await db.Repositories.AsNoTracking().FirstAsync()).Id;

        // 8000 characters exceeds what a plain nvarchar column would hold, so this fails loudly if
        // the column type ever changes away from nvarchar(max).
        var body = new string('x', 8_000);
        var item = new BacklogItem
        {
            RepositoryId       = repositoryId,
            Type               = Domain.Enums.BacklogItemType.UserStory,
            State              = Domain.Enums.BacklogItemState.New,
            Title              = "Long acceptance criteria",
            Rank               = 999_000m,
            AcceptanceCriteria = body,
        };

        db.BacklogItems.Add(item);
        await db.SaveChangesAsync();

        await using var verify = database.CreateContext();
        var reloaded = await verify.BacklogItems.AsNoTracking().SingleAsync(b => b.Id == item.Id);
        reloaded.AcceptanceCriteria.Should().HaveLength(8_000);

        // Leave the database as it was found — the fixture is shared.
        verify.BacklogItems.Remove(reloaded);
        await verify.SaveChangesAsync();
    }

    [Fact]
    public async Task TheSoftDeleteFilterIsAppliedByTheDatabaseQuery()
    {
        await using var db = database.CreateContext();

        var repositoryId = (await db.Repositories.AsNoTracking().FirstAsync()).Id;

        var task = new SprintTask
        {
            RepositoryId = repositoryId,
            Type         = Domain.Enums.SprintTaskType.Task,
            Title        = "Soft-delete probe",
        };
        db.SprintTasks.Add(task);
        await db.SaveChangesAsync();

        db.SprintTasks.Remove(task);
        await db.SaveChangesAsync();

        await using var verify = database.CreateContext();
        (await verify.SprintTasks.AnyAsync(t => t.Id == task.Id))
            .Should().BeFalse("the global query filter must translate into the SQL WHERE clause");
        (await verify.SprintTasks.IgnoreQueryFilters().AnyAsync(t => t.Id == task.Id))
            .Should().BeTrue("the row is kept, only hidden");

        var row = await verify.SprintTasks.IgnoreQueryFilters().SingleAsync(t => t.Id == task.Id);
        verify.SprintTasks.Remove(row);
        await verify.Database.ExecuteSqlRawAsync(
            "DELETE FROM SprintTasks WHERE Id = {0}", task.Id);
    }
}
