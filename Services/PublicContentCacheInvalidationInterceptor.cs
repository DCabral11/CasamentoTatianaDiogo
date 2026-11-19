using CasamentoTatianaDiogo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CasamentoTatianaDiogo.Services;

public sealed class PublicContentCacheInvalidationInterceptor(PublicContentCache cache) : SaveChangesInterceptor
{
    private bool changed;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        changed = eventData.Context?.ChangeTracker.Entries().Any(entry =>
            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
            entry.Entity is WeddingSettings or SiteContent or FaqPageSettings or FaqItem or TimelineEvent) == true;
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(SavingChanges(eventData, result));

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (changed) cache.Invalidate();
        changed = false;
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
        int result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(SavedChanges(eventData, result));
}
