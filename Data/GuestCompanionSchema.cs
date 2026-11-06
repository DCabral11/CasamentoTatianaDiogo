using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Data;

internal static class GuestCompanionSchema
{
    public static async Task UpgradeSqlServerAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'[Guests]', N'IsPlusOne') IS NULL
                ALTER TABLE [Guests] ADD [IsPlusOne] bit NOT NULL CONSTRAINT [DF_Guests_IsPlusOne] DEFAULT 0;
            IF COL_LENGTH(N'[Guests]', N'MainGuestId') IS NULL
                ALTER TABLE [Guests] ADD [MainGuestId] int NULL;
            IF COL_LENGTH(N'[PlusOnes]', N'CompanionGuestId') IS NULL
                ALTER TABLE [PlusOnes] ADD [CompanionGuestId] int NULL;
            IF COL_LENGTH(N'[PlusOnes]', N'IsArchived') IS NULL
                ALTER TABLE [PlusOnes] ADD [IsArchived] bit NOT NULL CONSTRAINT [DF_PlusOnes_IsArchived] DEFAULT 0;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Guests_Guests_MainGuestId')
                ALTER TABLE [Guests] ADD CONSTRAINT [FK_Guests_Guests_MainGuestId] FOREIGN KEY ([MainGuestId]) REFERENCES [Guests] ([Id]);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PlusOnes_Guests_CompanionGuestId')
                ALTER TABLE [PlusOnes] ADD CONSTRAINT [FK_PlusOnes_Guests_CompanionGuestId] FOREIGN KEY ([CompanionGuestId]) REFERENCES [Guests] ([Id]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Guests]') AND name = N'IX_Guests_MainGuestId')
                CREATE INDEX [IX_Guests_MainGuestId] ON [Guests] ([MainGuestId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[PlusOnes]') AND name = N'IX_PlusOnes_CompanionGuestId')
                CREATE UNIQUE INDEX [IX_PlusOnes_CompanionGuestId] ON [PlusOnes] ([CompanionGuestId]) WHERE [CompanionGuestId] IS NOT NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Guests_Companion')
                ALTER TABLE [Guests] ADD CONSTRAINT [CK_Guests_Companion] CHECK
                (([IsPlusOne] = 0 AND [MainGuestId] IS NULL) OR ([IsPlusOne] = 1 AND [MainGuestId] IS NOT NULL AND [MainGuestId] <> [Id] AND [AllowPlusOne] = 0));
            """);
    }
}
