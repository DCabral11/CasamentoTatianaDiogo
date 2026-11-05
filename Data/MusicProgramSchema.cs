using CasamentoTatianaDiogo.Models;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Data;

internal static class MusicProgramSchema
{
    private static readonly (string Moment, string? Song)[] Mass = [
        ("Entrada Noivo", "Ruínas"), ("Entrada Noiva", "Ninguém te Ama"),
        ("Ato Penitencial", "Perdoa-me Senhor"), ("Aleluia", "Quero Louvar-te"),
        ("Salmo", null), ("Alianças", "Deus de Amor (Amor tão Grande)"),
        ("Ofertório", "Corça"), ("Santo", "Africano"), ("Pai Nosso", "Pai tu és meu Deus"),
        ("Cordeiro", "Ama Vivendo + Cordeiro"), ("Comunhão", "Pedacinho de Deus"),
        ("Ação de Graças", "Cristo em Mim"), ("Consagração a Nossa Senhora", "Consagração a Nossa Senhora"),
        ("Final", "Sabes Bem")
    ];
    private const string MassTitle = "Cancioneiro Missa", ReceptionTitle = "Músicas Quinta";

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<MusicSection>().HasMany(s => s.Moments).WithOne()
            .HasForeignKey(m => m.SectionId).OnDelete(DeleteBehavior.Restrict);
        // Migration seed, not startup defaults: edits and deletions survive restarts.
        builder.Entity<MusicSection>().HasData(
            new MusicSection { Id = 1, Name = MassTitle, DisplayOrder = 10 },
            new MusicSection { Id = 2, Name = ReceptionTitle, DisplayOrder = 20 });
        builder.Entity<MusicMoment>().HasData(Mass.Select((item, i) => new MusicMoment
        {
            Id = i + 1, Name = item.Moment, Song = item.Song, SectionId = 1, DisplayOrder = (i + 1) * 10
        }));
    }

    // SQL Server installations created before this module use EnsureCreated instead of migrations.
    public static async Task UpgradeSqlServerAsync(ApplicationDbContext db)
    {
        if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE object_id = OBJECT_ID(N'[MusicSections]')").SingleAsync() != 0)
            return;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE [MusicSections] ([Id] int IDENTITY PRIMARY KEY, [Name] nvarchar(100) NOT NULL, [DisplayOrder] int NOT NULL);
                CREATE TABLE [MusicMoments] ([Id] int IDENTITY PRIMARY KEY, [SectionId] int NOT NULL REFERENCES [MusicSections]([Id]),
                    [Name] nvarchar(150) NOT NULL, [Song] nvarchar(250) NULL, [DisplayOrder] int NOT NULL);
                CREATE INDEX [IX_MusicMoments_SectionId] ON [MusicMoments] ([SectionId]);
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [MusicSections] ([Name], [DisplayOrder]) VALUES ({MassTitle}, 10), ({ReceptionTitle}, 20)");
            for (int i = 0; i < Mass.Length; i++)
            {
                var item = Mass[i];
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [MusicMoments] ([SectionId],[Name],[Song],[DisplayOrder]) SELECT [Id],{item.Moment},{item.Song},{(i + 1) * 10} FROM [MusicSections] WHERE [Name] = {MassTitle}");
            }
            await tx.CommitAsync();
        });
    }
}
