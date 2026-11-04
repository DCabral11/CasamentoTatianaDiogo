using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CasamentoTatianaDiogo.Data;

public static class DatabaseConfiguration
{
    private static readonly string[] RuntimeUrlKeys = ["DATABASE_URL", "POSTGRES_URL", "POSTGRES_PRISMA_URL"];
    private static readonly string[] DirectUrlKeys = ["DATABASE_URL_UNPOOLED", "POSTGRES_URL_NON_POOLING"];

    public static void AddWeddingDatabase(this IServiceCollection services, IConfiguration config, bool initializeDatabase = false)
    {
        var hasPostgresSettings = FirstConfigured(config, RuntimeUrlKeys) != null || FirstConfigured(config, DirectUrlKeys) != null ||
            FirstConfigured(config, "PGHOST", "PGHOST_UNPOOLED", "POSTGRES_HOST", "POSTGRES_HOST_UNPOOLED") != null;
        var provider = FirstConfigured(config, "Database:Provider") ?? (hasPostgresSettings ? "PostgreSql" : "SqlServer");
        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            var connection = ResolvePostgresConnection(config, initializeDatabase);
            services.AddDbContext<PostgreSqlApplicationDbContext>(options => options.UseNpgsql(connection, sql => sql.EnableRetryOnFailure()));
            services.AddScoped<ApplicationDbContext>(sp => sp.GetRequiredService<PostgreSqlApplicationDbContext>());
        }
        else if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var connection = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("A ligação à base de dados não está configurada.");
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure()));
        }
        else
            throw new InvalidOperationException("Database__Provider deve ser PostgreSql ou SqlServer.");
    }

    public static string ResolvePostgresConnection(IConfiguration config, bool initializeDatabase = false)
    {
        // Migration commands must bypass PgBouncer; web requests keep using the pooled URL.
        var connection = initializeDatabase ? FirstConfigured(config, DirectUrlKeys) : null;
        if (connection == null && initializeDatabase)
            connection = ConnectionFromParameters(config, unpooled: true);
        connection ??= FirstConfigured(config, RuntimeUrlKeys)
            ?? FirstConfigured(config, "ConnectionStrings:DefaultConnection")
            ?? ConnectionFromParameters(config, unpooled: false)
            ?? FirstConfigured(config, DirectUrlKeys)
            ?? ConnectionFromParameters(config, unpooled: true);
        if (connection == null)
            throw new InvalidOperationException("Configure DATABASE_URL, POSTGRES_URL, ConnectionStrings__DefaultConnection ou os parâmetros PGHOST/PGUSER/PGDATABASE/PGPASSWORD.");

        var settings = new NpgsqlConnectionStringBuilder(NormalizePostgresConnection(connection));
        if (initializeDatabase && IsNeonPooler(settings.Host))
            throw new InvalidOperationException("A inicialização do Neon requer uma ligação direta. Configure DATABASE_URL_UNPOOLED, POSTGRES_URL_NON_POOLING ou PGHOST_UNPOOLED com os restantes parâmetros PG*.");
        return settings.ConnectionString;
    }

    private static string? ConnectionFromParameters(IConfiguration config, bool unpooled)
    {
        var host = unpooled ? FirstConfigured(config, "PGHOST_UNPOOLED", "POSTGRES_HOST_UNPOOLED")
            : FirstConfigured(config, "PGHOST", "POSTGRES_HOST");
        if (host == null)
            return null;
        var user = FirstConfigured(config, "PGUSER", "POSTGRES_USER");
        var database = FirstConfigured(config, "PGDATABASE", "POSTGRES_DATABASE");
        var password = FirstConfigured(config, "PGPASSWORD", "POSTGRES_PASSWORD");
        if (user == null || database == null || password == null)
            throw new InvalidOperationException("Os parâmetros PostgreSQL requerem host, utilizador, base de dados e password (PG* ou POSTGRES_*).");

        var settings = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.Parse(FirstConfigured(config, "PGPORT", "POSTGRES_PORT") ?? "5432", System.Globalization.CultureInfo.InvariantCulture),
            Username = user,
            Database = database,
            Password = password,
            SslMode = Enum.Parse<SslMode>((FirstConfigured(config, "PGSSLMODE") ?? "verify-full").Replace("-", ""), true),
            ChannelBinding = Enum.Parse<ChannelBinding>(FirstConfigured(config, "PGCHANNELBINDING") ?? "require", true),
            MaxPoolSize = IsNeonPooler(host) ? 2 : 10,
            Timeout = int.Parse(FirstConfigured(config, "PGCONNECT_TIMEOUT") ?? "15", System.Globalization.CultureInfo.InvariantCulture)
        };
        return settings.ConnectionString;
    }

    private static string? FirstConfigured(IConfiguration config, params string[] keys) =>
        keys.Select(key => config[key]).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsNeonPooler(string? host) => host != null && host.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase) &&
        host.Contains("-pooler.", StringComparison.OrdinalIgnoreCase);

    public static string NormalizePostgresConnection(string connection)
    {
        if (!connection.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !connection.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return connection;

        var uri = new Uri(connection);
        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 2 || uri.AbsolutePath.Length <= 1)
            throw new InvalidOperationException("DATABASE_URL deve incluir utilizador, password e nome da base de dados.");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath[1..]),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            SslMode = SslMode.VerifyFull,
            MaxPoolSize = IsNeonPooler(uri.Host) ? 2 : 10,
            Timeout = 15
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var value = Uri.UnescapeDataString(parts.ElementAtOrDefault(1) ?? "");
            switch (parts[0].ToLowerInvariant())
            {
                case "sslmode": builder.SslMode = Enum.Parse<SslMode>(value.Replace("-", ""), true); break;
                case "channel_binding": builder.ChannelBinding = Enum.Parse<ChannelBinding>(value, true); break;
                case "connect_timeout": builder.Timeout = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
                case "application_name": builder.ApplicationName = value; break;
                case "options": builder.Options = value; break;
                default: throw new InvalidOperationException($"Parâmetro PostgreSQL não suportado: {parts[0]}. Use uma connection string Npgsql para opções adicionais.");
            }
        }
        return builder.ConnectionString;
    }
}
