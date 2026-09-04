using CasamentoTatianaDiogo.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<WeddingSettings> WeddingSettings => Set<WeddingSettings>();
        public DbSet<TimelineEvent> TimelineEvents => Set<TimelineEvent>();
        public DbSet<Family> Families => Set<Family>();
        public DbSet<Guest> Guests => Set<Guest>();
        public DbSet<PlusOne> PlusOnes => Set<PlusOne>();
        public DbSet<RsvpResponse> RsvpResponses => Set<RsvpResponse>();
        public DbSet<PhotoUploadSettings> PhotoUploadSettings => Set<PhotoUploadSettings>();
        public DbSet<PhotoUpload> PhotoUploads => Set<PhotoUpload>();
        public DbSet<SiteContent> SiteContents => Set<SiteContent>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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
        }
    }
}
