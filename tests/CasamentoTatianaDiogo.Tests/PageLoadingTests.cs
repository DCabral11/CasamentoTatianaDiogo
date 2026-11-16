using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
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
        }
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
