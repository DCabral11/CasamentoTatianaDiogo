using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Data;

internal static class FaqSchema
{
    public static Task UpgradeSqlServerAsync(ApplicationDbContext db) => db.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[FaqItems]', N'U') IS NULL
        BEGIN
            CREATE TABLE [FaqItems] (
                [Id] int IDENTITY NOT NULL PRIMARY KEY, [Question] nvarchar(300) NOT NULL,
                [Answer] nvarchar(max) NOT NULL, [DisplayOrder] int NOT NULL, [IsPublished] bit NOT NULL,
                [Version] uniqueidentifier NOT NULL
            );
            CREATE INDEX [IX_FaqItems_IsPublished_DisplayOrder] ON [FaqItems] ([IsPublished], [DisplayOrder]);
        END
        IF OBJECT_ID(N'[FaqPageSettings]', N'U') IS NULL
        BEGIN
            CREATE TABLE [FaqPageSettings] (
                [Id] int NOT NULL PRIMARY KEY, [IsEnabled] bit NOT NULL, [Title] nvarchar(150) NOT NULL,
                [NavigationLabel] nvarchar(40) NOT NULL, [Introduction] nvarchar(2000) NULL,
                [EmptyMessage] nvarchar(500) NOT NULL, [Version] uniqueidentifier NOT NULL
            );
            INSERT INTO [FaqPageSettings] ([Id], [IsEnabled], [Title], [NavigationLabel], [Introduction], [EmptyMessage], [Version])
            VALUES (1, 0, N'Perguntas frequentes', N'Perguntas frequentes',
                N'Encontra aqui as respostas às dúvidas sobre o nosso dia.',
                N'Estamos a preparar as respostas às perguntas mais frequentes. Volta em breve!',
                '00000000-0000-0000-0000-000000000000');
        END
        """);
}
