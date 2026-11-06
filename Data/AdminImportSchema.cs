using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Data;

internal static class AdminImportSchema
{
    public static Task UpgradeSqlServerAsync(ApplicationDbContext db) => db.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[AdminImportDrafts]', N'U') IS NULL
        BEGIN
            CREATE TABLE [AdminImportDrafts] (
                [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                [OwnerId] nvarchar(450) NOT NULL,
                [ImportType] nvarchar(20) NOT NULL,
                [RowsJson] nvarchar(max) NOT NULL,
                [PlanJson] nvarchar(max) NOT NULL,
                [Revision] uniqueidentifier NOT NULL,
                [ExpiresAt] datetime2 NOT NULL,
                [CompletedAt] datetime2 NULL
            );
            CREATE INDEX [IX_AdminImportDrafts_ExpiresAt] ON [AdminImportDrafts] ([ExpiresAt]);
        END
        """);
}
