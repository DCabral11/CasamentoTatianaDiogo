using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Data;

// Local SQL Server installations use EnsureCreated plus explicit, idempotent upgrades.
internal static class RsvpVerificationSchema
{
    public static Task UpgradeSqlServerAsync(ApplicationDbContext db) => db.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[RsvpInvitationContacts]', N'U') IS NULL
        BEGIN
            CREATE TABLE [RsvpInvitationContacts] (
                [FamilyId] int NOT NULL CONSTRAINT [PK_RsvpInvitationContacts] PRIMARY KEY,
                [Channel] int NOT NULL,
                [NormalizedContact] nvarchar(254) NOT NULL,
                [ConfirmedAt] datetime2 NOT NULL,
                CONSTRAINT [FK_RsvpInvitationContacts_Families_FamilyId] FOREIGN KEY ([FamilyId]) REFERENCES [Families] ([Id]) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX [IX_RsvpInvitationContacts_Channel_NormalizedContact] ON [RsvpInvitationContacts] ([Channel], [NormalizedContact]);
        END
        IF OBJECT_ID(N'[RsvpVerificationChallenges]', N'U') IS NULL
        BEGIN
            CREATE TABLE [RsvpVerificationChallenges] (
                [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_RsvpVerificationChallenges] PRIMARY KEY,
                [GuestId] int NOT NULL,
                [FamilyId] int NOT NULL,
                [Channel] int NOT NULL,
                [NormalizedContact] nvarchar(254) NOT NULL,
                [CodeHash] nvarchar(512) NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [ExpiresAt] datetime2 NOT NULL,
                [Attempts] int NOT NULL,
                [VerifiedAt] datetime2 NULL,
                [SessionExpiresAt] datetime2 NULL,
                CONSTRAINT [FK_RsvpVerificationChallenges_Guests_GuestId] FOREIGN KEY ([GuestId]) REFERENCES [Guests] ([Id]) ON DELETE CASCADE
            );
            CREATE INDEX [IX_RsvpVerificationChallenges_GuestId] ON [RsvpVerificationChallenges] ([GuestId]);
            CREATE INDEX [IX_RsvpVerificationChallenges_CreatedAt] ON [RsvpVerificationChallenges] ([CreatedAt]);
            CREATE INDEX [IX_RsvpVerificationChallenges_FamilyId_Channel_NormalizedContact] ON [RsvpVerificationChallenges] ([FamilyId], [Channel], [NormalizedContact]);
        END
        IF OBJECT_ID(N'[RsvpVerificationLimits]', N'U') IS NULL
        BEGIN
            CREATE TABLE [RsvpVerificationLimits] (
                [Id] nvarchar(64) NOT NULL CONSTRAINT [PK_RsvpVerificationLimits] PRIMARY KEY,
                [WindowEndsAt] datetime2 NOT NULL,
                [NextAllowedAt] datetime2 NOT NULL,
                [Count] int NOT NULL
            );
            CREATE INDEX [IX_RsvpVerificationLimits_WindowEndsAt] ON [RsvpVerificationLimits] ([WindowEndsAt]);
        END
        """);
}
