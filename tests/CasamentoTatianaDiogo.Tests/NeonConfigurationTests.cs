using CasamentoTatianaDiogo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class NeonConfigurationTests
{
    // Synthetic endpoint and password: no production credentials belong in these tests.
    private const string PooledHost = "ep-test-example-pooler.c-15.us-east-1.aws.neon.tech";
    private const string DirectHost = "ep-test-example.c-15.us-east-1.aws.neon.tech";
    private const string PooledUrl = $"postgresql://neondb_owner:test-password@{PooledHost}/neondb?channel_binding=require&sslmode=require";
    private const string DirectUrl = $"postgresql://neondb_owner:test-password@{DirectHost}/neondb?sslmode=require";

    [Theory]
    [InlineData("DATABASE_URL")]
    [InlineData("POSTGRES_URL")]
    [InlineData("POSTGRES_PRISMA_URL")]
    public void RuntimeRecognizesNeonUrlAliasesAndRequiredChannelBinding(string key)
    {
        var settings = Resolve(new() { [key] = PooledUrl + "&connect_timeout=15" });
        Assert.Equal(PooledHost, settings.Host);
        Assert.Equal("neondb_owner", settings.Username);
        Assert.Equal("neondb", settings.Database);
        Assert.Equal(SslMode.Require, settings.SslMode);
        Assert.Equal(ChannelBinding.Require, settings.ChannelBinding);
        Assert.Equal(15, settings.Timeout);
        Assert.Equal(2, settings.MaxPoolSize);
    }

    [Theory]
    [InlineData("DATABASE_URL_UNPOOLED")]
    [InlineData("POSTGRES_URL_NON_POOLING")]
    public void InitializationUsesDirectConnectionWhileRuntimeKeepsPooler(string key)
    {
        var values = new Dictionary<string, string?> { ["DATABASE_URL"] = PooledUrl, [key] = DirectUrl };
        Assert.Equal(PooledHost, Resolve(values).Host);
        Assert.Equal(DirectHost, Resolve(values, initialize: true).Host);
        Assert.Equal(SslMode.Require, Resolve(values, initialize: true).SslMode);
    }

    [Theory]
    [InlineData("PGHOST", "PGHOST_UNPOOLED", "PGUSER", "PGPASSWORD", "PGDATABASE")]
    [InlineData("POSTGRES_HOST", "POSTGRES_HOST_UNPOOLED", "POSTGRES_USER", "POSTGRES_PASSWORD", "POSTGRES_DATABASE")]
    public void ComponentVariablesPreservePasswordsAndUseDirectHostForSetup(string hostKey, string directKey, string userKey, string passwordKey, string databaseKey)
    {
        var values = new Dictionary<string, string?>
        {
            [hostKey] = PooledHost, [directKey] = DirectHost,
            [userKey] = "neondb_owner", [passwordKey] = "test;password=with:symbols", [databaseKey] = "neondb"
        };
        var pooled = Resolve(values);
        Assert.Equal(PooledHost, pooled.Host);
        Assert.Equal("test;password=with:symbols", pooled.Password);
        Assert.Equal(SslMode.VerifyFull, pooled.SslMode);
        Assert.Equal(ChannelBinding.Require, pooled.ChannelBinding);
        Assert.Equal(DirectHost, Resolve(values, initialize: true).Host);
    }

    [Fact]
    public void ExplicitUrlsTakePrecedenceAndBlankAliasesAreIgnored()
    {
        var values = new Dictionary<string, string?> { ["DATABASE_URL"] = " ", ["POSTGRES_URL"] = PooledUrl, ["POSTGRES_URL_NO_SSL"] = "invalid" };
        Assert.Equal(PooledHost, Resolve(values).Host);
        values["DATABASE_URL"] = DirectUrl;
        Assert.Equal(DirectHost, Resolve(values).Host);
    }

    [Fact]
    public void NeonMigrationsRejectPooledConnectionWhenNoDirectConnectionIsConfigured()
    {
        var config = Config(new() { ["DATABASE_URL"] = PooledUrl });
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.ResolvePostgresConnection(config, initializeDatabase: true));
        Assert.Contains("DATABASE_URL_UNPOOLED", error.Message);
        Assert.DoesNotContain("test-password", error.Message);
    }

    [Fact]
    public void InitializationRejectsAnIncorrectlyPooledDirectUrl()
    {
        var config = Config(new() { ["DATABASE_URL_UNPOOLED"] = PooledUrl });
        Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.ResolvePostgresConnection(config, initializeDatabase: true));
    }

    [Fact]
    public void ProviderDetectionUsesNeonAliasesWithoutAnExplicitProvider()
    {
        var services = new ServiceCollection();
        services.AddWeddingDatabase(Config(new() { ["POSTGRES_URL"] = PooledUrl, ["POSTGRES_URL_NON_POOLING"] = DirectUrl }), initializeDatabase: true);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.IsType<PostgreSqlApplicationDbContext>(db);
        Assert.Equal(DirectHost, new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()).Host);
    }

    [Fact]
    public void IncompleteComponentConfigurationFailsWithAnActionableError()
    {
        var config = Config(new() { ["PGHOST"] = PooledHost });
        Assert.Contains("PG*", Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.ResolvePostgresConnection(config)).Message);
    }

    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    private static NpgsqlConnectionStringBuilder Resolve(Dictionary<string, string?> values, bool initialize = false) =>
        new(DatabaseConfiguration.ResolvePostgresConnection(Config(values), initialize));
}
