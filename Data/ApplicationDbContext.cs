using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        protected ApplicationDbContext(DbContextOptions options) : base(options) { }

        public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
        public DbSet<PendingPhotoUpload> PendingPhotoUploads => Set<PendingPhotoUpload>();
        public DbSet<RsvpInvitationContact> RsvpInvitationContacts => Set<RsvpInvitationContact>();
        public DbSet<RsvpVerificationChallenge> RsvpVerificationChallenges => Set<RsvpVerificationChallenge>();
        public DbSet<RsvpVerificationLimit> RsvpVerificationLimits => Set<RsvpVerificationLimit>();

        public DbSet<WeddingSettings> WeddingSettings => Set<WeddingSettings>();
        public DbSet<TimelineEvent> TimelineEvents => Set<TimelineEvent>();
        public DbSet<Family> Families => Set<Family>();
        public DbSet<Guest> Guests => Set<Guest>();
        public DbSet<PlusOne> PlusOnes => Set<PlusOne>();
        public DbSet<RsvpResponse> RsvpResponses => Set<RsvpResponse>();
        public DbSet<PhotoUploadSettings> PhotoUploadSettings => Set<PhotoUploadSettings>();
        public DbSet<PhotoUpload> PhotoUploads => Set<PhotoUpload>();
        public DbSet<SiteContent> SiteContents => Set<SiteContent>();
        public DbSet<PlanningCategory> PlanningCategories => Set<PlanningCategory>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
        public DbSet<MusicSection> MusicSections => Set<MusicSection>();
        public DbSet<MusicMoment> MusicMoments => Set<MusicMoment>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            MusicProgramSchema.Configure(builder);

            builder.Entity<PlanningCategory>().HasIndex(x => x.NormalizedName).IsUnique();
            builder.Entity<Supplier>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<BudgetLine>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<BudgetLine>().HasOne(x => x.Supplier).WithMany(x => x.Lines)
                .HasForeignKey(x => new { x.SupplierId, x.CategoryId }).HasPrincipalKey(x => new { x.Id, x.CategoryId })
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<WeddingSettings>().HasIndex(x => x.Id).IsUnique();
            builder.Entity<SiteContent>().HasIndex(x => x.Key).IsUnique();

            builder.Entity<TimelineEvent>().HasIndex(x => new { x.IsActive, x.DisplayOrder, x.EventDateTime });

            builder.Entity<Family>().HasIndex(x => x.Name);
            builder.Entity<Family>().HasIndex(x => x.GroupCode).IsUnique().HasFilter("[GroupCode] IS NOT NULL");

            builder.Entity<Guest>().HasIndex(x => new { x.FirstName, x.LastName });
            builder.Entity<Guest>().HasOne(x => x.Family).WithMany(x => x.Guests).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Restrict);
            
            builder.Entity<PlusOne>().HasOne(x => x.MainGuest).WithMany(x => x.PlusOnes).HasForeignKey(x => x.MainGuestId).OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RsvpResponse>().HasIndex(x => new { x.GuestId, x.SubmittedAt });
            builder.Entity<RsvpResponse>().HasOne(x => x.Guest).WithMany(x => x.RsvpResponses).HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<RsvpResponse>().HasOne(x => x.PlusOne).WithMany().HasForeignKey(x => x.PlusOneId).OnDelete(DeleteBehavior.NoAction);

            builder.Entity<PhotoUpload>().HasIndex(x => new { x.UploadedAt });
            builder.Entity<PendingPhotoUpload>().HasIndex(x => x.ExpiresAt);
            builder.Entity<PendingPhotoUpload>().HasOne(x => x.PhotoUpload).WithMany().HasForeignKey(x => x.PhotoUploadId).OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RsvpInvitationContact>().HasKey(x => x.FamilyId);
            builder.Entity<RsvpInvitationContact>().HasIndex(x => new { x.Channel, x.NormalizedContact }).IsUnique();
            builder.Entity<RsvpInvitationContact>().HasOne<Family>().WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<RsvpVerificationChallenge>().HasOne<Guest>().WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<RsvpVerificationChallenge>().HasIndex(x => x.CreatedAt);
            builder.Entity<RsvpVerificationChallenge>().HasIndex(x => new { x.FamilyId, x.Channel, x.NormalizedContact });
            builder.Entity<RsvpVerificationLimit>().HasIndex(x => x.WindowEndsAt);
        }
    }
}
