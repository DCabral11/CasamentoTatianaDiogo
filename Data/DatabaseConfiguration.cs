using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CasamentoTatianaDiogo.Data;

public static class DatabaseConfiguration
{
    public static void AddWeddingDatabase(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Database:Provider"] ?? (string.IsNullOrWhiteSpace(config["DATABASE_URL"]) ? "SqlServer" : "PostgreSql");
        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            var connection = config["DATABASE_URL"] ?? config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("Configure DATABASE_URL ou ConnectionStrings__DefaultConnection para PostgreSQL.");

            connection = NormalizePostgresConnection(connection);
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
            MaxPoolSize = 10,
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
