using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data
{
    public class RaceDayDbContext : DbContext
    {
        public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Enrolment> Enrolments => Set<Enrolment>();
        public DbSet<Result> Results => Set<Result>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>(e =>
            {
                e.ToTable("Role");
                e.Property(x => x.RoleName).HasMaxLength(20);
                e.HasIndex(x => x.RoleName).IsUnique();
            });

            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("User");
                e.Property(x => x.FullName).HasMaxLength(100);
                e.Property(x => x.Email).HasMaxLength(150);
                e.HasIndex(x => x.Email).IsUnique();
                e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasOne(x => x.Role).WithMany(r => r.Users)
                    .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Event>(e =>
            {
                e.ToTable("Event", t =>
                {
                    t.HasCheckConstraint("CK_Event_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')");
                    t.HasCheckConstraint("CK_Event_Distance", "[Distance] > 0");
                });
                e.Property(x => x.Name).HasMaxLength(150);
                e.Property(x => x.Location).HasMaxLength(150);
                e.Property(x => x.EventType).HasMaxLength(20);
                e.Property(x => x.Distance).HasPrecision(6, 2);
                e.HasOne(x => x.Organiser).WithMany(u => u.Events)
                    .HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Category>(e =>
            {
                e.ToTable("Category", t =>
                {
                    t.HasCheckConstraint("CK_Category_Age",
                        "[MinAge] IS NULL OR [MaxAge] IS NULL OR [MinAge] <= [MaxAge]");
                });
                e.Property(x => x.Name).HasMaxLength(50);
                e.Property(x => x.CategoryDistance).HasPrecision(6, 2);
                e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
                    .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Enrolment>(e =>
            {
                e.ToTable("Enrolment");
                e.Property(x => x.EnrolmentDate).HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => new { x.ParticipantId, x.EventId }).IsUnique();
                e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                    .HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Event).WithMany(ev => ev.Enrolments)
                    .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                    .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Result>(e =>
            {
                e.ToTable("Result");
                e.Property(x => x.RecordedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => x.EnrolmentId).IsUnique();
                e.HasOne(x => x.Enrolment).WithOne(en => en.Result)
                    .HasForeignKey<Result>(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}