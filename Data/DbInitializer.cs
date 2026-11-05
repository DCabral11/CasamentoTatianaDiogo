using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, IHostEnvironment environment)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();

            if (db.Database.IsNpgsql())
                await db.Database.MigrateAsync();
            else
            {
                await db.Database.EnsureCreatedAsync();

                // EnsureCreated does not apply model changes to databases that already exist.
                // Keep this upgrade idempotent so the RSVP form can save the additional
                // accompaniment details without requiring a manual database intervention.
                await db.Database.ExecuteSqlRawAsync("""
                    IF COL_LENGTH(N'[RsvpResponses]', N'PlusOneDietaryRestrictions') IS NULL
                        ALTER TABLE [RsvpResponses] ADD [PlusOneDietaryRestrictions] nvarchar(1000) NULL;
                    IF COL_LENGTH(N'[RsvpResponses]', N'PlusOneMessage') IS NULL
                        ALTER TABLE [RsvpResponses] ADD [PlusOneMessage] nvarchar(1000) NULL;
                    IF COL_LENGTH(N'[RsvpResponses]', N'PlusOneMusicRequest') IS NULL
                        ALTER TABLE [RsvpResponses] ADD [PlusOneMusicRequest] nvarchar(200) NULL;
                    IF COL_LENGTH(N'[Guests]', N'AvatarFileName') IS NULL
                        ALTER TABLE [Guests] ADD [AvatarFileName] nvarchar(255) NULL;
                    IF COL_LENGTH(N'[Guests]', N'AvatarScale') IS NULL
                        ALTER TABLE [Guests] ADD [AvatarScale] float NOT NULL CONSTRAINT [DF_Guests_AvatarScale] DEFAULT 1;
                    IF COL_LENGTH(N'[Guests]', N'AvatarPositionX') IS NULL
                        ALTER TABLE [Guests] ADD [AvatarPositionX] int NOT NULL CONSTRAINT [DF_Guests_AvatarPositionX] DEFAULT 50;
                    IF COL_LENGTH(N'[Guests]', N'AvatarPositionY') IS NULL
                        ALTER TABLE [Guests] ADD [AvatarPositionY] int NOT NULL CONSTRAINT [DF_Guests_AvatarPositionY] DEFAULT 50;
                    """);

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

                await db.Database.ExecuteSqlRawAsync("""
                    IF OBJECT_ID(N'[DataProtectionKeys]', N'U') IS NULL
                        CREATE TABLE [DataProtectionKeys] ([Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY, [FriendlyName] nvarchar(max) NULL, [Xml] nvarchar(max) NULL);
                    IF OBJECT_ID(N'[PendingPhotoUploads]', N'U') IS NULL
                    BEGIN
                        CREATE TABLE [PendingPhotoUploads] (
                            [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                            [UploadUri] nvarchar(4000) NOT NULL,
                            [DriveFileId] nvarchar(200) NOT NULL,
                            [OriginalFileName] nvarchar(255) NOT NULL,
                            [ContentType] nvarchar(100) NOT NULL,
                            [SizeBytes] bigint NOT NULL,
                            [UploadedBytes] bigint NOT NULL,
                            [GuestName] nvarchar(220) NULL,
                            [Notes] nvarchar(1000) NULL,
                            [UploadIp] nvarchar(64) NULL,
                            [ExpiresAt] datetime2 NOT NULL,
                            [PhotoUploadId] int NULL REFERENCES [PhotoUploads] ([Id]) ON DELETE CASCADE
                        );
                        CREATE INDEX [IX_PendingPhotoUploads_ExpiresAt] ON [PendingPhotoUploads] ([ExpiresAt]);
                        CREATE INDEX [IX_PendingPhotoUploads_PhotoUploadId] ON [PendingPhotoUploads] ([PhotoUploadId]);
                    END
                    """);
            }

            if (db.Database.IsSqlServer())
                await RsvpVerificationSchema.UpgradeSqlServerAsync(db);

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            if (!await roleManager.RoleExistsAsync("Admin"))
                EnsureSuccess(await roleManager.CreateAsync(new IdentityRole("Admin")));

            var email = config["AdminSeed:Email"] ?? (environment.IsDevelopment() ? "admin@tatiana-diogo.local" : null);
            var password = config["AdminSeed:Password"] ?? (environment.IsDevelopment() ? "ChangeMe!2027Wedding" : null);
            var admin = string.IsNullOrWhiteSpace(email) ? null : await userManager.FindByEmailAsync(email);

            if (admin == null && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };

                var result = await userManager.CreateAsync(admin, password);

                EnsureSuccess(result);
            }

            if (admin != null && !await userManager.IsInRoleAsync(admin, "Admin"))
                EnsureSuccess(await userManager.AddToRoleAsync(admin, "Admin"));
            if (!environment.IsDevelopment() && (await userManager.GetUsersInRoleAsync("Admin")).Count == 0)
                throw new InvalidOperationException("Configure AdminSeed__Email e AdminSeed__Password antes de inicializar uma base de dados nova.");

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
                    new SiteContent { Key = "NavHome", Value = "Início", Description = "Navegação: Página Inicial" },
                    new SiteContent { Key = "NavInformation", Value = "Informações", Description = "Navegação: Informações" },
                    new SiteContent { Key = "NavRsvp", Value = "Confirmar Presença", Description = "Navegação: Confirmação de Presença" },
                    new SiteContent { Key = "NavPhotos", Value = "Enviar fotografias", Description = "Navegação: envio de fotografias" },
                    new SiteContent { Key = "HomeStoryTitle", Value = "A Nossa História", Description = "Página Inicial: Título da História" },
                    new SiteContent { Key = "HomeRsvpCta", Value = "Confirmar Presença", Description = "Página Inicial: Botão de Confirmação" },
                    new SiteContent { Key = "FooterText", Value = "Feito com Amor por Diogo", Description = "Rodapé do site" },
                    new SiteContent { Key = "PhotosNamePlaceholder", Value = "O teu Nome (Opcional)", Description = "Galeria: campo de nome" },
                    new SiteContent { Key = "PhotosMessagePlaceholder", Value = "Mensagem (Opcional)", Description = "Galeria: campo de mensagem" });
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

            if (environment.IsDevelopment() && !await db.Families.AnyAsync())
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

        private static void EnsureSuccess(IdentityResult result)
        {
            if (!result.Succeeded)
                throw new InvalidOperationException("Não foi possível preparar o administrador: " + string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }
}
