using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class PageLoadingTests
{
    [Fact]
    public async Task HomeReadsSharedSettingsAndLabelsOncePerRequestAndShowsEditsImmediately()
    {
        var queries = new QueryLog();
        using var app = new HttpTests.TestApplication(queries: queries);
        using var client = app.Client();
        for (var version = 1; version <= 2; version++)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var footer = db.SiteContents.SingleOrDefault(x => x.Key == "FooterText");
            footer ??= db.SiteContents.Add(new SiteContent { Key = "FooterText", Description = "Footer" }).Entity;
            footer.Value = $"Rodape {version}";
            await db.SaveChangesAsync();
            queries.Commands.Clear();
            Assert.Contains(footer.Value, await client.GetStringAsync("/"));
            Assert.Single(queries.Commands, sql => sql.Contains("FROM \"SiteContents\""));
            Assert.Single(queries.Commands, sql => sql.Contains("FROM \"WeddingSettings\""));
            queries.Commands.Clear();
            Assert.Contains(footer.Value, await client.GetStringAsync("/"));
            Assert.Empty(queries.Commands);
        }
    }

    [Fact]
    public async Task InformationAndPublishedFaqReusePresentationDataAcrossVisits()
    {
        var queries = new QueryLog();
        using var app = new HttpTests.TestApplication(queries: queries);
        using var client = app.Client();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.FaqPageSettings.SingleAsync()).IsEnabled = true;
            db.FaqItems.Add(new FaqItem { Question = "Public question", Answer = "Public answer", IsPublished = true });
            db.TimelineEvents.Add(new TimelineEvent { Title = "Ceremony", EventDateTime = new(2027, 6, 5, 11, 0, 0) });
            await db.SaveChangesAsync();
        }
        Assert.Contains("Ceremony", await client.GetStringAsync("/Information"));
        Assert.Contains("Public answer", await client.GetStringAsync("/Faq"));
        queries.Commands.Clear();
        Assert.Contains("Ceremony", await client.GetStringAsync("/Information"));
        Assert.Contains("Public answer", await client.GetStringAsync("/Faq"));
        Assert.Empty(queries.Commands);
    }

    [Fact]
    public async Task MusicEntryPagesDoNotLoadTheirInnerPageModels()
    {
        var queries = new QueryLog();
        using var app = new HttpTests.TestApplication(queries: queries);
        using var client = app.Client();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.WeddingSettings.SingleAsync()).SpotifyTrackUrl = "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT";
            (await db.FaqPageSettings.SingleAsync()).IsEnabled = true;
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        queries.Commands.Clear();
        foreach (var route in new[] { "/", "/Information", "/Photos", "/Faq", "/Rsvp" })
            Assert.Contains("id=\"guest-page\"", await client.GetStringAsync(route));
        Assert.DoesNotContain(queries.Commands, sql => sql.Contains("\"TimelineEvents\"") ||
            sql.Contains("\"PhotoUploadSettings\"") || sql.Contains("\"FaqItems\"") || sql.Contains("\"SiteContents\""));

        queries.Commands.Clear();
        Assert.DoesNotContain("id=\"guest-page\"", await client.GetStringAsync("/Information?sitePage=1"));
        Assert.Single(queries.Commands, sql => sql.Contains("\"TimelineEvents\""));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Information/Calendar")).StatusCode);
        var error = await client.GetAsync("/Home/Error");
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.DoesNotContain("id=\"guest-page\"", await error.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AssetsAreCacheableWithoutCachingRsvpPagesOrReadingTheDatabase()
    {
        var queries = new QueryLog();
        using var app = new HttpTests.TestApplication(queries: queries);
        using var client = app.Client();
        queries.Commands.Clear();
        var script = await client.GetAsync("/js/rsvp-select.js?v=content-hash");
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.True(script.Headers.CacheControl!.Public);
        Assert.Equal(TimeSpan.FromDays(365), script.Headers.CacheControl.MaxAge);
        Assert.Contains("immutable", script.Headers.CacheControl.ToString());
        Assert.Contains("s-maxage=31536000", Assert.Single(script.Headers.GetValues("Vercel-CDN-Cache-Control")));
        Assert.NotNull(script.Headers.ETag);
        using var conditional = new HttpRequestMessage(HttpMethod.Get, "/js/rsvp-select.js?v=content-hash");
        conditional.Headers.IfNoneMatch.Add(script.Headers.ETag);
        var unchanged = await client.SendAsync(conditional);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
        Assert.True(unchanged.Headers.CacheControl!.Public);

        var image = await client.GetAsync("/images/navbar.png");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal(TimeSpan.FromHours(1), image.Headers.CacheControl!.MaxAge);
        Assert.Contains("s-maxage=86400", Assert.Single(image.Headers.GetValues("Vercel-CDN-Cache-Control")));
        Assert.Empty(queries.Commands);

        var rsvp = await client.GetAsync("/Rsvp");
        Assert.True(rsvp.Headers.CacheControl!.NoStore);
        Assert.False(rsvp.Headers.Contains("Vercel-CDN-Cache-Control"));
    }

    [Fact]
    public async Task RsvpDeadlineAndPhotoPermissionsBypassThePresentationCache()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        await client.GetStringAsync("/");
        await client.GetStringAsync("/Photos");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Simulate a change from another instance, without local invalidation.
            await db.WeddingSettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.RsvpDeadline, new DateOnly(2000, 1, 1)));
            await db.PhotoUploadSettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsEnabled, false));
        }
        Assert.DoesNotContain("guest-search", await client.GetStringAsync("/Rsvp"));
        Assert.Contains("O envio de fotografias n", await client.GetStringAsync("/Photos"));
        using var fresh = app.Services.CreateScope();
        var settings = fresh.ServiceProvider.GetRequiredService<CasamentoTatianaDiogo.Services.Interfaces.IWeddingSettingsService>();
        Assert.Null((await settings.GetPresentationAsync()).RsvpDeadline);
        Assert.Equal(new DateOnly(2000, 1, 1), (await settings.GetAsync()).RsvpDeadline);
    }

    [Fact]
    public async Task GuestAndRsvpPagesLimitRowsAndExportEveryFilteredGuest()
    {
        var queries = new QueryLog();
        using var app = new HttpTests.TestApplication(queries: queries);
        using var client = app.Client();
        await ExcelImportTests.Login(app, client);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var family = new Family { Name = "Álvares" };
        for (var n = 1; n <= 55; n++)
            db.Guests.Add(new Guest { Family = family, FirstName = "Teste", LastName = n.ToString("D3"),
                DisplayName = $"Pessoa {n:D3}", IsChild = n <= 53 });
        await db.SaveChangesAsync();

        queries.Commands.Clear();
        var first = await client.GetStringAsync("/Admin/Rsvp/Index");
        Assert.Contains("Pessoa 050", first);
        Assert.DoesNotContain("Pessoa 051", first);
        var listQuery = Assert.Single(queries.Commands, sql => sql.Contains("\"RsvpResponses\""));
        Assert.Contains("EXISTS", listQuery);
        Assert.Contains("LIMIT", listQuery);
        Assert.DoesNotContain("DietaryRestrictions", listQuery);

        var filter = $"?q=alvares+pessoa&familyId={family.Id}&child=yes";
        var second = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Guests/Index" + filter + "&page=99"));
        Assert.Contains("Página 2 de 2", second);
        Assert.Contains("53 de 55 registos", second);
        Assert.Contains("Pessoa 051", second);
        Assert.Contains("Pessoa 053", second);
        Assert.DoesNotContain("Pessoa 050", second);
        Assert.DoesNotContain("Pessoa 054", second);
        var csv = await client.GetStringAsync("/Admin/Guests/ExportCsv" + filter + "&page=2");
        Assert.Contains("Pessoa 001", csv);
        Assert.Contains("Pessoa 053", csv);
        Assert.DoesNotContain("Pessoa 054", csv);
        Assert.Contains("1 de 1 registos", await client.GetStringAsync("/Admin/InvitationGroups/Index?q=pessoa+053&status=pending"));
    }

    [Fact]
    public async Task PhotoDateFilterIncludesBothEndsOfTheLisbonSummerTimeDay()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        await ExcelImportTests.Login(app, client);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = new DateTime(2026, 3, 29, 0, 0, 0, DateTimeKind.Utc);
        foreach (var (name, date) in new[] { ("before", start.AddSeconds(-1)), ("first", start),
                     ("last", start.AddHours(23).AddSeconds(-1)), ("after", start.AddHours(23)) })
            db.PhotoUploads.Add(new PhotoUpload { OriginalFileName = name + ".jpg", ContentType = "image/jpeg",
                GoogleDriveFileId = name, UploadedAt = date });
        await db.SaveChangesAsync();
        var html = await client.GetStringAsync("/Admin/Photos/Index?from=2026-03-29&until=2026-03-29&type=image%2Fjpeg");
        Assert.Contains("first.jpg", html);
        Assert.Contains("last.jpg", html);
        Assert.DoesNotContain("before.jpg", html);
        Assert.DoesNotContain("after.jpg", html);
    }

    private sealed class QueryLog : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
