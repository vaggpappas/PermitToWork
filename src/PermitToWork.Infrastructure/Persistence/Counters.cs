using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PermitToWork.Infrastructure.Persistence;

/// <summary>
/// A named running number. One row per sequence — <c>employee:ACME</c>, <c>team:MEC-2026</c>.
/// </summary>
internal sealed class Counter
{
    public string Key { get; set; } = null!;
    public int Value { get; set; }
}

internal sealed class CounterConfiguration : IEntityTypeConfiguration<Counter>
{
    public void Configure(EntityTypeBuilder<Counter> builder)
    {
        builder.ToTable("Counters");
        builder.HasKey(c => c.Key);
        builder.Property(c => c.Key).HasMaxLength(64);
        builder.Property(c => c.Value).IsRequired();
    }
}

/// <summary>
/// Hands out the next number in a named sequence, safely under concurrency.
/// <para>
/// The obvious implementation — read the highest existing number, add one — has a race:
/// two requests read the same maximum and both propose the same badge. The unique index
/// then turns one of them into a 500. Rare, and exactly the kind of thing that only
/// happens during a demo.
/// </para>
/// <para>
/// This is a single atomic statement instead. <c>MERGE … WITH (HOLDLOCK)</c> takes a range
/// lock on the key, so a concurrent call blocks rather than reading a stale value, and
/// <c>OUTPUT</c> returns the number that was actually written. Insert-or-increment in one
/// round trip, with no read-then-write window for anything to slip through.
/// </para>
/// <para>
/// Numbers can still be skipped — if the surrounding <c>SaveChanges</c> fails after this
/// runs, that number is spent. That is deliberate: badge numbers must be unique, and
/// nothing anywhere requires them to be contiguous. Guaranteeing no gaps would mean holding
/// a lock across the whole transaction, which trades a cosmetic property for a real one.
/// </para>
/// </summary>
internal sealed class CounterStore(PermitToWorkDbContext context)
{
    private const string NextValueSql = """
        MERGE org.Counters WITH (HOLDLOCK) AS target
        USING (SELECT {0} AS [Key]) AS source
        ON target.[Key] = source.[Key]
        WHEN MATCHED THEN UPDATE SET [Value] = target.[Value] + 1
        WHEN NOT MATCHED THEN INSERT ([Key], [Value]) VALUES ({0}, 1)
        OUTPUT inserted.[Value];
        """;

    public async Task<int> NextAsync(string key, CancellationToken cancellationToken = default)
    {
        var values = await context.Database
            .SqlQueryRaw<int>(NextValueSql, key)
            .ToListAsync(cancellationToken);

        return values.Single();
    }
}
