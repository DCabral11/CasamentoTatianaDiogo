using Microsoft.Extensions.Caching.Memory;

namespace CasamentoTatianaDiogo.Services;

// Only presentation data belongs here. Access checks, RSVP deadlines and photo
// upload permissions must continue to read their authoritative database values.
public sealed class PublicContentCache(IMemoryCache memory, TimeProvider time) : IDisposable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim loading = new(1, 1);
    private long generation;

    public async Task<T> GetAsync<T>(string name, Func<Task<T>> load) where T : class
    {
        var key = (typeof(PublicContentCache), Interlocked.Read(ref generation), name);
        if (TryRead(key, out T? value)) return value!;

        // Do not retain a Task or a scoped DbContext in the shared cache. A cold
        // instance loads each value once, even when several visits arrive together.
        await loading.WaitAsync();
        try
        {
            key = (typeof(PublicContentCache), Interlocked.Read(ref generation), name);
            if (TryRead(key, out value)) return value!;
            var loaded = await load();
            memory.Set(key, new CachedValue<T>(loaded, time.GetUtcNow() + Lifetime), Lifetime);
            return loaded;
        }
        finally { loading.Release(); }
    }

    // An in-flight read can finish after an edit. Its old generation remains
    // unreachable, so it cannot repopulate the current cache with stale content.
    public void Invalidate() => Interlocked.Increment(ref generation);

    private bool TryRead<T>(object key, out T? value) where T : class
    {
        if (memory.TryGetValue(key, out CachedValue<T>? cached) && cached != null && cached.ExpiresAt > time.GetUtcNow())
        {
            value = cached.Value;
            return true;
        }
        value = null;
        return false;
    }

    public void Dispose() => loading.Dispose();
    private sealed record CachedValue<T>(T Value, DateTimeOffset ExpiresAt);
}
