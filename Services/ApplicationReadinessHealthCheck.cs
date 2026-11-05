using CasamentoTatianaDiogo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CasamentoTatianaDiogo.Services;

public class ApplicationReadinessHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Não foi possível ligar à base de dados.");

            if (db.Database.IsNpgsql() && (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                return HealthCheckResult.Unhealthy("A base de dados tem migrações pendentes. Execute --initialize-database na base usada pelo deploy.");

            // A successful connection alone does not mean that the app's tables or seed data exist.
            var settings = await db.WeddingSettings.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            var photos = await db.PhotoUploadSettings.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            await db.SiteContents.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            await db.DataProtectionKeys.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            await db.PendingPhotoUploads.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            await db.RsvpInvitationContacts.AsNoTracking().Select(x => x.FamilyId).Take(1).ToListAsync(cancellationToken);
            await db.RsvpVerificationChallenges.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            await db.RsvpVerificationLimits.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            if (settings.Count == 0 || photos.Count == 0)
                return HealthCheckResult.Unhealthy("A configuração inicial está incompleta. Execute --initialize-database.");

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("As tabelas da aplicação não estão prontas. Execute --initialize-database e consulte os logs.", ex);
        }
    }
}
