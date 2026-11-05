using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class HttpTests
{
    [Fact]
    public async Task SavedThemeReachesGuestAdminAndRecoveryPages()
    {
        using var app = new TestApplication();
        using var client = app.Client();
        await client.GetStringAsync("/"); // Warm the layout with the original settings.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await db.WeddingSettings.SingleAsync();
            settings.LogoPath = "/images/custom-logo.svg";
            settings.PrimaryColor = "#fcf7e8";
            settings.SecondaryColor = "#183929";
            await db.SaveChangesAsync();
        }

        foreach (var path in new[] { "/", "/Information", "/Rsvp", "/Photos", "/Admin/Login", "/pagina-inexistente" })
        {
            var response = await client.GetAsync(path);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("src=\"/images/custom-logo.svg\"", html);
            Assert.Contains("--primary:#FCF7E8;", html);
            Assert.Contains("--secondary:#183929;", html);
            Assert.Contains("--on-primary:#000000;", html);
            Assert.Contains("data-bs-theme=\"dark\"", html);
        }
    }

    [Fact]
    public async Task ProductionPagesAndHealthChecksWorkWithoutStartupSchemaChanges()
    {
        using var app = new TestApplication();
        using var client = app.Client();
        foreach (var route in new[] { "/", "/Information", "/Rsvp", "/Photos", "/health", "/health/live" })
        {
            var response = await client.GetAsync(route);
            Assert.True(response.IsSuccessStatusCode, $"{route}: {response.StatusCode}");
        }
    }

    [Fact]
    public async Task MissingDatabaseSchemaShowsARecoveryPageInsteadOfAnEmpty500()
    {
        using var app = new TestApplication(initializeSchema: false);
        using var client = app.Client();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Algo não correu como esperado", WebUtility.HtmlDecode(body));
        Assert.DoesNotContain("SqliteException", body);
        Assert.DoesNotContain("ConnectionStrings", body);
    }

    [Fact]
    public async Task DatabaseWithoutSchemaFailsReadinessWhileTheProcessRemainsLive()
    {
        using var app = new TestApplication(initializeSchema: false);
        using var client = app.Client();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task MissingSeedSettingsFailReadiness()
    {
        using var app = new TestApplication();
        using var client = app.Client();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.WeddingSettings.RemoveRange(db.WeddingSettings);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task NotFoundPageStillRendersWhenDatabaseTablesAreMissing()
    {
        using var app = new TestApplication(initializeSchema: false);
        using var client = app.Client();
        var response = await client.GetAsync("/pagina-inexistente");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Esta página já não está aqui", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task HttpUploadRequiresAntiforgeryAndAcceptsALargePhotoInSmallRequests()
    {
        using var app = new TestApplication();
        using var client = app.Client();
        var metadata = new BeginPhotoUploadRequest { FileName = "photo.jpg", ContentType = "image/jpeg", SizeBytes = 6 * 1024 * 1024 };
        var denied = await client.PostAsJsonAsync("/Photos/BeginUpload", metadata);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        var token = await AntiforgeryToken(client, "/Photos");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        var begin = await client.PostAsJsonAsync("/Photos/BeginUpload", metadata);
        Assert.Equal(HttpStatusCode.OK, begin.StatusCode);
        var ticket = (await begin.Content.ReadFromJsonAsync<PhotoUploadTicket>())!;
        for (long offset = 0; offset < metadata.SizeBytes; offset += ticket.ChunkSize)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Photos/UploadChunk")
            {
                Content = new ByteArrayContent(new byte[ticket.ChunkSize])
            };
            request.Headers.Add("X-Upload-Token", ticket.Token);
            request.Headers.Add("X-Upload-Offset", offset.ToString());
            var chunk = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, chunk.StatusCode);
        }
        using var scope = app.Services.CreateScope();
        Assert.Equal(metadata.SizeBytes, (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PhotoUploads.SingleAsync()).SizeBytes);
    }

    [Fact]
    public async Task ProductionAdminCanLoginWithSecureCookies()
    {
        using var app = new TestApplication();
        using var client = app.Client();
        using (var scope = app.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "admin@example.test", Email = "admin@example.test", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, "TestPassword!2027")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        }
        var token = await AntiforgeryToken(client, "/Admin/Login");
        var response = await client.PostAsync("/Admin/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "admin@example.test", ["password"] = "TestPassword!2027", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.Contains(".AspNetCore.Identity.Application=") && cookie.Contains("secure", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin/Dashboard/Index")).StatusCode);
    }

    internal static async Task<string> AntiforgeryToken(HttpClient client, string route)
    {
        var html = await client.GetStringAsync(route);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        return WebUtility.HtmlDecode(token.Groups[1].Value);
    }

    internal class TestApplication : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private readonly bool schemaReady;
        private readonly IRsvpVerificationSender? verificationSender;
        public TestApplication(bool initializeSchema = true, IRsvpVerificationSender? verificationSender = null)
        {
            connection.Open();
            schemaReady = initializeSchema;
            this.verificationSender = verificationSender;
        }
        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SqlServer",
                ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=true"
            }));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SqlServer",
                ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=true"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
                services.RemoveAll<IGoogleDriveService>();
                services.AddSingleton<IGoogleDriveService, HttpDrive>();
                if (verificationSender != null)
                {
                    services.RemoveAll<IRsvpVerificationSender>();
                    services.AddSingleton(verificationSender);
                }
                if (schemaReady)
                {
                    using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
                    db.Database.EnsureCreated();
                    db.WeddingSettings.Add(new WeddingSettings());
                    db.PhotoUploadSettings.Add(new PhotoUploadSettings { IsEnabled = true, GoogleDriveFolderId = "test-folder" });
                    db.SaveChanges();
                }
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) connection.Dispose();
        }
    }

    private class HttpDrive : IGoogleDriveService
    {
        public Task<DriveUploadSession> BeginUploadAsync(string name, string type, long size, string folder, CancellationToken ct) =>
            Task.FromResult(new DriveUploadSession("https://www.googleapis.com/upload/test", "test-id"));
        public Task<DriveChunkResult> UploadChunkAsync(string uri, string id, Stream stream, long offset, long length, long total, CancellationToken ct) =>
            Task.FromResult(new DriveChunkResult(offset + length, offset + length == total ? new DriveUploadResult(id, "https://drive.google.com/test") : null));
        public Task<DriveUploadResult> UploadAsync(Stream stream, string name, string type, string folder, CancellationToken ct) => throw new NotSupportedException();
    }
}
