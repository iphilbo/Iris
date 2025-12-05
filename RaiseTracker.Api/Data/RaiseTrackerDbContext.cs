using Microsoft.EntityFrameworkCore;
using RaiseTracker.Api.Models;

namespace RaiseTracker.Api.Data;

public class RaiseTrackerDbContext : DbContext
{
    public RaiseTrackerDbContext(DbContextOptions<RaiseTrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Investor> Investors { get; set; }
    public DbSet<InvestorTask> InvestorTasks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.Username).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // Configure Investor entity
        modelBuilder.Entity<Investor>(entity =>
        {
            entity.ToTable("Investors");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.MainContact).HasMaxLength(256);
            entity.Property(e => e.ContactEmail).HasMaxLength(256);
            entity.Property(e => e.ContactPhone).HasMaxLength(50);
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Stage).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired().HasDefaultValue("Active");
            entity.Property(e => e.CommitAmount).HasColumnType("DECIMAL(18,2)");
            entity.Property(e => e.CreatedBy).HasMaxLength(450);
            entity.Property(e => e.UpdatedBy).HasMaxLength(450);

            // Configure RowVersion for optimistic concurrency
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsRequired();

            // Configure relationship with tasks
            entity.HasMany(e => e.Tasks)
                .WithOne()
                .HasForeignKey(t => t.InvestorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.UpdatedAt);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Stage);
        });

        // Configure InvestorTask entity
        modelBuilder.Entity<InvestorTask>(entity =>
        {
            entity.ToTable("InvestorTasks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.InvestorId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.DueDate).HasMaxLength(10).IsRequired(); // YYYY-MM-DD format

            // Foreign key relationship is configured in Investor entity
            entity.HasIndex(e => e.InvestorId);
        });

        // Ignore Tasks navigation property in Investor for EF (we'll handle it manually)
        // Actually, we want to keep it, so we'll configure it properly above
    }
}
