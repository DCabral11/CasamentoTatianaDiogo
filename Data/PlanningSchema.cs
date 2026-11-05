using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Data;

internal static class PlanningSchema
{
    // Existing SQL Server databases use EnsureCreated and explicit upgrades.
    public static Task UpgradeSqlServerAsync(ApplicationDbContext db) => db.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[PlanningCategories]', N'U') IS NULL
        BEGIN
            CREATE TABLE [PlanningCategories] (
                [Id] int IDENTITY PRIMARY KEY, [Name] nvarchar(100) NOT NULL,
                [NormalizedName] nvarchar(150) NOT NULL UNIQUE
            );
        END
        IF OBJECT_ID(N'[Suppliers]', N'U') IS NULL
        BEGIN
            CREATE TABLE [Suppliers] (
                [Id] int IDENTITY PRIMARY KEY, [CategoryId] int NOT NULL REFERENCES [PlanningCategories]([Id]),
                [Name] nvarchar(150) NOT NULL, [Contact] nvarchar(150) NULL, [Email] nvarchar(254) NULL,
                [Website] nvarchar(500) NULL, [Status] nvarchar(40) NOT NULL, [Notes] nvarchar(2000) NULL,
                [Price] decimal(14,2) NULL, UNIQUE ([Id], [CategoryId])
            );
            CREATE INDEX [IX_Suppliers_CategoryId] ON [Suppliers] ([CategoryId]);
        END
        IF OBJECT_ID(N'[BudgetLines]', N'U') IS NULL
        BEGIN
            CREATE TABLE [BudgetLines] (
                [Id] int IDENTITY PRIMARY KEY, [CategoryId] int NOT NULL REFERENCES [PlanningCategories]([Id]),
                [SupplierId] int NULL, [RealCost] decimal(14,2) NULL, [Paid] decimal(14,2) NOT NULL,
                [Status] nvarchar(40) NOT NULL, [NextPayment] date NULL, [Notes] nvarchar(2000) NULL,
                FOREIGN KEY ([SupplierId], [CategoryId]) REFERENCES [Suppliers] ([Id], [CategoryId])
            );
            CREATE INDEX [IX_BudgetLines_CategoryId] ON [BudgetLines] ([CategoryId]);
            CREATE INDEX [IX_BudgetLines_SupplierId_CategoryId] ON [BudgetLines] ([SupplierId], [CategoryId]);
        END
        """);
}
