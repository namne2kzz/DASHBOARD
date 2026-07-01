---
name: efcore-sqlserver
description: Use when writing or optimizing EF Core queries/configuration against SQL Server or Azure SQL — indexing, fluent API config, query plan-driven fixes.
---

# Skill: EF Core + SQL Server

Patterns, optimization, and best practices for EF Core with SQL Server / Azure SQL.

## Usage
```
/efcore-sqlserver [query|config|index|optimize] [context]
```

---

## Prompt Templates

### Query Optimization
```
Optimize this EF Core + SQL Server query:

**Current LINQ**:
[Paste EF Core query]

**Generated SQL** (optional — from EF Core logging or SSMS):
[Paste SQL if available]

**Performance Metrics**:
- Current duration: [ms]
- Row count: [number]
- Execution frequency: [times/min]
- Target duration: [ms]

**Table Schema** (relevant columns):
[List columns and data types]

**Existing Indexes**:
[List current indexes or "unknown"]

Identify and fix:
1. N+1 queries
2. Missing AsNoTracking() on reads
3. Unnecessary Include() loading extra data
4. Missing projections (Select to DTO instead of loading full entity)
5. Inefficient WHERE conditions (not index-friendly)
6. Missing index recommendations
7. Pagination issues (Skip/Take on large sets)

Provide:
- Optimized LINQ code
- SQL equivalent for review
- Index creation T-SQL
- Expected improvement estimate
```

### Entity Configuration
```
Write EF Core 10 entity configuration for:

**Entity**: [EntityName]
**Table**: [table_name]

**Properties**:
- [Property]: [Type], [constraints: required/optional, max length, precision]
- [Property]: [Type], [constraints]

**Relationships**:
- [Relationship type]: [other entity], [FK column], [cascade]

**Owned entities / Value Objects**:
- [VO name]: [properties]

**Indexes**:
- [Columns]: [purpose — e.g., "for WHERE status AND customer_id"]

Use IEntityTypeConfiguration<T> pattern (no DataAnnotations).
```

### Index Strategy
```
Design SQL Server indexing strategy for:

**Table**: [table_name]
**Row count**: [estimated]

**Query patterns** (list top 5 queries):
1. WHERE [columns], ORDER BY [columns], SELECT [columns]
2. [same format]

**Write pattern**: [Insert/Update/Delete frequency]
**Current indexes**: [list or "none known"]

Provide:
1. Recommended indexes with T-SQL
2. Clustered vs non-clustered decision
3. Composite index column order (selectivity rule)
4. Included columns for covering indexes
5. Write overhead estimate
6. Index maintenance plan
```

---

## Configuration Patterns

### DbContext Setup
```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.HasDefaultSchema("app");
    }
}

// Registration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sql => sql
            .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), null)
            .CommandTimeout(30)
    )
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)  // global no-tracking
);
```

### Read Queries (Optimized)
```csharp
// ✅ Projection — never load full entity for reads
var orders = await db.Orders
    .Where(o => o.CustomerId == customerId && o.Status == OrderStatus.Active)
    .OrderByDescending(o => o.CreatedAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(o => new OrderSummaryDto(
        o.Id, o.CreatedAt, o.TotalAmount,
        o.Items.Count, o.Customer.FullName))
    .ToListAsync(ct);

// ✅ Single entity with specific navigation
var order = await db.Orders
    .Where(o => o.Id == orderId)
    .Include(o => o.Items)  // only load what's needed
    .FirstOrDefaultAsync(ct);
```

### Bulk Operations (EF Core 7+)
```csharp
// Bulk update — no entity loading
await db.Orders
    .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Expired), ct);

// Bulk delete
await db.OrderItems
    .Where(i => i.OrderId == orderId)
    .ExecuteDeleteAsync(ct);
```

## Common Index Patterns
```sql
-- Composite: filter by status, sort by date
CREATE INDEX IX_Orders_Status_CreatedAt
ON Orders (Status, CreatedAt DESC)
INCLUDE (CustomerId, TotalAmount);

-- Covering index for frequent projection
CREATE INDEX IX_Orders_CustomerId_Active
ON Orders (CustomerId, Status)
WHERE Status = 'Active'
INCLUDE (Id, TotalAmount, CreatedAt);
```
