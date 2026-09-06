using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("A ligação à base de dados não está configurada.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;

    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
}).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Admin/Login";
    options.AccessDeniedPath = "/Admin/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.SlidingExpiration = true;
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");

if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
{
    var keyDirectory = Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? builder.Environment.ContentRootPath, "data", "protection-keys");
    Directory.CreateDirectory(keyDirectory);
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyDirectory)).SetApplicationName("CasamentoTatianaDiogo");
}

builder.Services.AddScoped<IWeddingSettingsService, WeddingSettingsService>();
builder.Services.AddScoped<IRsvpService, RsvpService>();
builder.Services.AddScoped<IRsvpEmailNotificationService, RsvpEmailNotificationService>();
builder.Services.AddScoped<IGoogleDriveService, GoogleDriveService>();
builder.Services.AddScoped<IPhotoUploadService, PhotoUploadService>();
builder.Services.AddScoped<ISiteContentService, SiteContentService>();
builder.Services.AddSingleton<IAppMessageService, AppMessageService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await DbInitializer.InitializeAsync(scope.ServiceProvider, app.Configuration, app.Environment);

// The same friendly recovery screen is used locally and after publishing.
app.UseExceptionHandler("/Home/Error");

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();
