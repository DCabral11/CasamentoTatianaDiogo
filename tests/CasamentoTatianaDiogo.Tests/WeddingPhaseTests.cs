using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class WeddingPhaseTests
{
    [Theory]
    [InlineData(2027, 6, 5, 10, 23)]
    [InlineData(2027, 1, 5, 11, 0)]
    public void StartAndDefaultEndUseWeddingLocalTime(int year, int month, int day, int startHourUtc, int endHourUtc)
    {
        var time = new MutableTime();
        var clock = new WeddingClock(time, new ConfigurationBuilder().Build());
        var date = new DateTime(year, month, day, 11, 0, 0);
        var settings = new WeddingSettings { WeddingDateTime = date };
        var initial = clock.GetPeriod(settings);
        Assert.Equal(startHourUtc, initial.StartUtc!.Value.Hour);
        Assert.Equal(endHourUtc, initial.EndUtc!.Value.Hour);
        Assert.Equal(date.Date.AddDays(1), clock.ToLocal(initial.EndUtc.Value));
        time.Advance(initial.StartUtc.Value.AddSeconds(-1) - time.GetUtcNow().UtcDateTime);
        Assert.Equal("Before", clock.GetPeriod(settings).Phase);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("During", clock.GetPeriod(settings).Phase);
        time.Advance(initial.EndUtc.Value - initial.StartUtc.Value);
        Assert.Equal("After", clock.GetPeriod(settings).Phase);
        Assert.Equal("Before", clock.GetPeriod(new WeddingSettings()).Phase);
    }

    [Fact]
    public async Task AdminMessagesFollowThePublicPageAndSwitchAtTheConfiguredInstants()
    {
        var time = new MutableTime();
        using var app = new HttpTests.TestApplication(time: time);
        using var client = app.Client();
        const string path = "/Admin/Content/PhaseMessages";
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(path)).StatusCode);
        await ExcelImportTests.Login(app, client);
        var form = new Dictionary<string, string>
        {
            ["page"] = "Information", ["guidance[Wedding.Information.Before]"] = "Antes personalizado",
            ["guidance[Wedding.Information.During]"] = "Durante personalizado",
            ["guidance[Wedding.Information.After]"] = "Depois personalizado",
            ["__RequestVerificationToken"] = await HttpTests.AntiforgeryToken(client, path + "?page=Information")
        };
        var saved = await client.PostAsync(path, new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Contains("Durante personalizado", await client.GetStringAsync(saved.Headers.Location));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.WeddingSettings.SingleAsync();
        settings.WeddingDateTime = new DateTime(2027, 6, 5, 11, 0, 0);
        settings.WeddingEndDateTime = new DateTime(2027, 6, 6, 2, 0, 0);
        await db.SaveChangesAsync();
        var start = new DateTimeOffset(2027, 6, 5, 10, 0, 0, TimeSpan.Zero);
        time.Advance(start.AddSeconds(-9) - time.GetUtcNow());
        var home = await client.GetStringAsync("/");
        Assert.Contains($"data-start=\"{start.ToUnixTimeMilliseconds()}\"", home);
        Assert.Contains("data-seconds>09<", home);
        Assert.Contains(">Antes personalizado</p>", await client.GetStringAsync("/Information"));
        time.Advance(TimeSpan.FromSeconds(9));
        Assert.DoesNotContain("id=\"countdown\"", await client.GetStringAsync("/"));
        Assert.Contains(">Durante personalizado</p>", await client.GetStringAsync("/Information"));
        Assert.DoesNotContain("Durante personalizado", await client.GetStringAsync("/Rsvp"));
        Assert.Contains("guest-search", await client.GetStringAsync("/Rsvp"));
        time.Advance(TimeSpan.FromHours(15));
        Assert.Contains(">Depois personalizado</p>", await client.GetStringAsync("/Information"));
        Assert.Contains("DTEND:20270606T010000Z", await client.GetStringAsync("/Information/Calendar"));
    }
}
