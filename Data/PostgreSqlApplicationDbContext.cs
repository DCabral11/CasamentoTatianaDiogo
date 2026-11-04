using CasamentoTatianaDiogo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CasamentoTatianaDiogo.Data;

// Separate context keeps PostgreSQL migrations independent of the existing SQL Server history.
public class PostgreSqlApplicationDbContext(DbContextOptions<PostgreSqlApplicationDbContext> options)
    : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Family>().HasIndex(x => x.GroupCode).HasFilter(null);

        // Wedding schedules are Lisbon wall-clock times. Audit timestamps remain UTC/timestamptz.
        builder.Entity<WeddingSettings>().Property(x => x.WeddingDateTime).HasColumnType("timestamp without time zone");
        builder.Entity<TimelineEvent>().Property(x => x.EventDateTime).HasColumnType("timestamp without time zone");
        builder.Entity<PhotoUploadSettings>().Property(x => x.OpensAt).HasColumnType("timestamp without time zone");
        builder.Entity<PhotoUploadSettings>().Property(x => x.ClosesAt).HasColumnType("timestamp without time zone");
    }
}

public class PostgreSqlDesignTimeFactory : IDesignTimeDbContextFactory<PostgreSqlApplicationDbContext>
{
    public PostgreSqlApplicationDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=casamento;Username=postgres;Password=design-time-only")
            .Options);
}
