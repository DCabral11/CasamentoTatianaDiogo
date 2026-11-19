using CasamentoTatianaDiogo.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class PublicContentCacheTests
{
    [Fact]
    public async Task ConcurrentVisitsShareOneLoadAndRefreshAfterThirtySeconds()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var time = new MutableTime();
        using var cache = new PublicContentCache(memory, time);
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task<string> Load() { calls++; return pending.Task; }
        var first = cache.GetAsync("text", Load);
        var second = cache.GetAsync("text", Load);
        pending.SetResult("original");
        Assert.Equal(new[] { "original", "original" }, await Task.WhenAll(first, second));
        Assert.Equal(1, calls);
        time.Advance(PublicContentCache.Lifetime - TimeSpan.FromMilliseconds(1));
        Assert.Equal("original", await cache.GetAsync("text", () => Task.FromResult("too early")));
        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal("updated", await cache.GetAsync("text", () => Task.FromResult("updated")));
    }

    [Fact]
    public async Task AnEditDuringLoadingCannotRepopulateTheCurrentCacheWithOldContent()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new PublicContentCache(memory, TimeProvider.System);
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var beforeEdit = cache.GetAsync("text", () => pending.Task);
        cache.Invalidate();
        pending.SetResult("old");
        Assert.Equal("old", await beforeEdit);
        Assert.Equal("new", await cache.GetAsync("text", () => Task.FromResult("new")));
        Assert.Equal("new", await cache.GetAsync("text", () => Task.FromResult("unexpected reload")));
    }

    [Fact]
    public async Task FailedLoadsAreRetriedOnTheNextVisit()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new PublicContentCache(memory, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync<string>("text",
            () => Task.FromException<string>(new InvalidOperationException("temporary database failure"))));
        Assert.Equal("recovered", await cache.GetAsync("text", () => Task.FromResult("recovered")));
    }
}
