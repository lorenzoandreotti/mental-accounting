using MentalAccounting.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MentalAccounting.Infrastructure;

public sealed class AccountRow
{
    public int Id { get; set; } = 1;
    public long Version { get; set; }
    public string Payload { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
public sealed class AccountingDbContext(DbContextOptions<AccountingDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<AccountRow> Accounts => Set<AccountRow>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<AccountRow>(b =>
        {
            b.ToTable("Accounts", t => t.HasCheckConstraint("CK_Accounts_Singleton", "\"Id\" = 1"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Payload).HasColumnType("jsonb");
        });
    }
}
public sealed class PostgresAccountStore(AccountingDbContext db, TimeProvider time) : IAccountStore
{
    public async Task<AccountSnapshot> Load(CancellationToken ct = default)
    {
        var row = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(ct);
        return row is null ? new(0, new(1, null, [])) : new(row.Version, BackupFormat.Read(row.Payload));
    }
    public async Task Save(AccountDocument document, long expectedVersion, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.ChangeTracker.Clear();
        var row = await db.Accounts.SingleOrDefaultAsync(ct);
        if ((row?.Version ?? 0) != expectedVersion) throw new ConcurrencyException();
        if (row is null)
        {
            row = new AccountRow { CreatedUtc = time.GetUtcNow().UtcDateTime };
            db.Accounts.Add(row);
        }
        row.Version = expectedVersion + 1;
        row.UpdatedUtc = time.GetUtcNow().UtcDateTime;
        row.Payload = BackupFormat.Write(document);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyException(); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { throw new ConcurrencyException(); }
    }
}
