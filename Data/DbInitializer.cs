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

            if (!await db.WeddingSettings.AnyAsync())
                db.WeddingSettings.Add(new WeddingSettings
                {
                    WeddingDateTime = new DateTime(2027, 6, 5, 11, 0, 0),
                    CoupleStory = "",
                    CeremonyLocationName = "Igreja de São Pedro",
                    CeremonyAddress = "Alverca",
                    ReceptionLocationName = "Quinta da Tareca",
                    ReceptionAddress = "Colares, Sintra"
                });

            if (!await db.PhotoUploadSettings.AnyAsync())
                db.PhotoUploadSettings.Add(new PhotoUploadSettings
                {
                    IsEnabled = false,
                    OpensAt = new DateTime(2027, 6, 5, 0, 0, 0),
                    ClosesAt = new DateTime(2027, 6, 6, 23, 59, 59)
                });

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
