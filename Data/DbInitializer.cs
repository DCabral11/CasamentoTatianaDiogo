using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();

            await db.Database.EnsureCreatedAsync();

            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[SiteContents]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [SiteContents] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [Key] nvarchar(100) NOT NULL,
                        [Value] nvarchar(max) NOT NULL,
                        [Description] nvarchar(300) NOT NULL,
                        [IsHtml] bit NOT NULL
                    );
                    CREATE UNIQUE INDEX [IX_SiteContents_Key] ON [SiteContents] ([Key]);
                END
                """);

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            if (!await roleManager.RoleExistsAsync("Admin"))
                await roleManager.CreateAsync(new IdentityRole("Admin"));

            var email = config["AdminSeed:Email"] ?? "admin@tatiana-diogo.local";
            var password = config["AdminSeed:Password"] ?? "ChangeMe!2027Wedding";
            var admin = await userManager.FindByEmailAsync(email);

            if (admin == null)
            {
                admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };

                var result = await userManager.CreateAsync(admin, password);

                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }

            var settings = await db.WeddingSettings.FirstOrDefaultAsync();

            if (settings == null)
                db.WeddingSettings.Add(new WeddingSettings
                {
                    WeddingDateTime = new DateTime(2027, 6, 5, 11, 0, 0),
                    CoupleStory = "",
                    CeremonyLocationName = "Igreja de São Pedro",
                    CeremonyAddress = "Alverca",
                    ReceptionLocationName = "Quinta da Tareca",
                    ReceptionAddress = "Colares, Sintra"
                });
            else if (settings.PrimaryColor == "#8f5f76" && settings.SecondaryColor == "#f6eee9")
            {
                // Upgrade only the original palette, preserving any colors customized by an administrator.
                settings.PrimaryColor = "#bd4742";
                settings.SecondaryColor = "#fce8e4";
            }

            if (!await db.PhotoUploadSettings.AnyAsync())
                db.PhotoUploadSettings.Add(new PhotoUploadSettings
                {
                    IsEnabled = false,
                    OpensAt = new DateTime(2027, 6, 5, 0, 0, 0),
                    ClosesAt = new DateTime(2027, 6, 6, 23, 59, 59)
                });

            if (!await db.SiteContents.AnyAsync())
            {
                db.SiteContents.AddRange(
                    new SiteContent { Key = "NavHome", Value = "Início", Description = "Navegação: página inicial" },
                    new SiteContent { Key = "NavInformation", Value = "Informações", Description = "Navegação: informações" },
                    new SiteContent { Key = "NavRsvp", Value = "Confirmar presença", Description = "Navegação: confirmação de presença" },
                    new SiteContent { Key = "NavPhotos", Value = "Galeria", Description = "Navegação: galeria" },
                    new SiteContent { Key = "HomeStoryTitle", Value = "A nossa história", Description = "Página inicial: título da história" },
                    new SiteContent { Key = "HomeRsvpCta", Value = "Confirmar presença", Description = "Página inicial: botão de confirmação" },
                    new SiteContent { Key = "FooterText", Value = "Feito com amor por Diogo", Description = "Rodapé do site" },
                    new SiteContent { Key = "PhotosNamePlaceholder", Value = "O teu nome (opcional)", Description = "Galeria: campo de nome" },
                    new SiteContent { Key = "PhotosMessagePlaceholder", Value = "Mensagem (opcional)", Description = "Galeria: campo de mensagem" });
            }

            if (!await db.TimelineEvents.AnyAsync())
            {
                var date = new DateTime(2027, 6, 5);

                db.TimelineEvents.AddRange(
                    new TimelineEvent { Title = "Chegada dos Convidados", EventDateTime = date.AddHours(10), DisplayOrder = 1, IconName = "bi-clock" },
                    new TimelineEvent { Title = "Cerimónia", EventDateTime = date.AddHours(11), DisplayOrder = 2, IconName = "bi-heart" },
                    new TimelineEvent { Title = "Cocktail", EventDateTime = date.AddHours(14), DisplayOrder = 3, IconName = "bi-cup-straw" },
                    new TimelineEvent { Title = "Almoço", EventDateTime = date.AddHours(15), DisplayOrder = 4, IconName = "bi-calendar-heart" },
                    new TimelineEvent { Title = "Festa", EventDateTime = date.AddHours(20), DisplayOrder = 5, IconName = "bi-music-note-beamed" }
                    );
            }

            if (!await db.Families.AnyAsync())
            {
                var testFamily = new Family
                {
                    Name = "Família Teste",
                    AllowGroupRsvp = true,
                    GroupCode = "TEST123"
                };

                db.Families.Add(testFamily);
                
                await db.SaveChangesAsync();

                db.Guests.AddRange(new Guest
                {
                    FamilyId = testFamily.Id,
                    FirstName = "João",
                    LastName = "Silva",
                    DisplayName = "João Silva",
                    AllowPlusOne = true,
                    CurrentStatus = RsvpStatus.Pending
                },
                new Guest
                {
                    FamilyId = testFamily.Id,
                    FirstName = "Maria",
                    LastName = "Silva",
                    DisplayName = "Maria Silva",
                    CurrentStatus = RsvpStatus.Pending
                });
            }
            
            await db.SaveChangesAsync();
        }
    }
}
