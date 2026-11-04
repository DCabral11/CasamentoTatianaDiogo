using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class DeploymentTests
{
    [Fact]
    public void PostgresUrlPreservesEncodedCredentialsAndTlsOptions()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.NormalizePostgresConnection(
            "postgresql://wedding%40user:p%3Ass%2Fword@example.com:5433/wedding%20db?sslmode=verify-full&channel_binding=require"));
        Assert.Equal("wedding@user", result.Username);
        Assert.Equal("p:ss/word", result.Password);
        Assert.Equal("wedding db", result.Database);
        Assert.Equal(5433, result.Port);
        Assert.Equal(SslMode.VerifyFull, result.SslMode);
        Assert.Equal(ChannelBinding.Require, result.ChannelBinding);
        Assert.Equal(10, result.MaxPoolSize);
    }

    [Fact]
    public void PostgresModelUsesPortableIndexesAndCorrectTimestampTypes()
    {
        using var db = new PostgreSqlDesignTimeFactory().CreateDbContext([]);
        var script = db.Database.GenerateCreateScript();
        Assert.DoesNotContain("[GroupCode]", script);
        Assert.Contains("timestamp without time zone", script);
        Assert.Contains("timestamp with time zone", script);
        Assert.Contains("DataProtectionKeys", script);
        Assert.Contains("PendingPhotoUploads", script);
        Assert.Equal("timestamp without time zone", db.Model.FindEntityType(typeof(WeddingSettings))!.FindProperty("WeddingDateTime")!.GetColumnType());
        Assert.Equal("timestamp with time zone", db.Model.FindEntityType(typeof(PhotoUpload))!.FindProperty("UploadedAt")!.GetColumnType());
        Assert.Single(db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData("2027-06-05T00:30:00Z", 1)]
    [InlineData("2027-12-05T00:30:00Z", 0)]
    public void WeddingClockUsesLisbonSummerAndWinterTime(string utc, int expectedHour)
    {
        var clock = new WeddingClock(new FixedTime(DateTimeOffset.Parse(utc)), new ConfigurationBuilder().Build());
        Assert.Equal(expectedHour, clock.Now.Hour);
    }

    [Fact]
    public async Task SeparateInstancesCanReadEachOthersProtectedCookies()
    {
        var path = Path.Combine(Path.GetTempPath(), $"casamento-keys-{Guid.NewGuid():N}.db");
        ServiceProvider BuildInstance()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={path};Pooling=False"));
            services.AddDataProtection().SetApplicationName("CasamentoTatianaDiogo").PersistKeysToDbContext<ApplicationDbContext>();
            return services.BuildServiceProvider();
        }
        try
        {
            using var first = BuildInstance();
            using (var scope = first.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
            var protectedValue = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("authentication-cookie").Protect("admin-session");
            using var second = BuildInstance();
            Assert.Equal("admin-session", second.GetRequiredService<IDataProtectionProvider>().CreateProtector("authentication-cookie").Unprotect(protectedValue));
        }
        finally { File.Delete(path); }
    }
}

internal class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
