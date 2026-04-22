using Microsoft.EntityFrameworkCore;
using RhemaBibleAppServerless.Domain.Models;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class RhemaDbContext(DbContextOptions<RhemaDbContext> options) : DbContext(options)
{
  public DbSet<User> Users => Set<User>();
  public DbSet<Note> Notes => Set<Note>();
  public DbSet<SavedVerse> SavedVerses => Set<SavedVerse>();
  public DbSet<RecentActivity> RecentActivities => Set<RecentActivity>();
  public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
  public DbSet<ProcessedWebhook> ProcessedWebhooks => Set<ProcessedWebhook>();
  public DbSet<ProcessedServiceBusDelivery> ProcessedServiceBusDeliveries => Set<ProcessedServiceBusDelivery>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<User>(e =>
    {
      e.ToTable("users");
      e.HasKey(x => x.Id);
      e.Property(x => x.Id).HasMaxLength(64);
      e.Property(x => x.Email).IsRequired().HasMaxLength(320);
      e.HasIndex(x => x.Email).IsUnique();
      e.Property(x => x.Password).IsRequired();
      e.Property(x => x.FirstName).HasMaxLength(200);
      e.Property(x => x.LastName).HasMaxLength(200);
      e.Property(x => x.ImageUrl).HasMaxLength(2000);
      e.Property(x => x.RefreshToken).HasMaxLength(2000);
      e.Property(x => x.SubscriptionType).HasConversion<string>().HasMaxLength(64);
      e.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
      e.Property(x => x.AiFreeCallsMonthKey).HasMaxLength(32);
      e.Property(x => x.IsDeleted).HasDefaultValue(false);
      e.Property(x => x.DeletedAt);
      e.HasIndex(x => x.IsDeleted);
    });

    modelBuilder.Entity<Note>(e =>
    {
      e.ToTable("notes");
      e.HasKey(x => x.Id);
      e.Property(x => x.Id).HasMaxLength(64);
      e.Property(x => x.AuthId).IsRequired().HasMaxLength(64);
      e.Property(x => x.Reference).IsRequired().HasMaxLength(500);
      e.Property(x => x.Text).IsRequired();
      e.HasIndex(x => x.AuthId);
      e.HasIndex(x => new { x.AuthId, x.CreatedAt });
    });

    modelBuilder.Entity<SavedVerse>(e =>
    {
      e.ToTable("saved_verses");
      e.HasKey(x => x.Id);
      e.Property(x => x.Id).HasMaxLength(64);
      e.Property(x => x.AuthId).IsRequired().HasMaxLength(64);
      e.Property(x => x.Reference).IsRequired().HasMaxLength(500);
      e.Property(x => x.Text).IsRequired();
      e.HasIndex(x => new { x.Reference, x.AuthId }).IsUnique();
    });

    modelBuilder.Entity<RecentActivity>(e =>
    {
      e.ToTable("recent_activities");
      e.HasKey(x => x.Id);
      e.Property(x => x.Id).HasMaxLength(64);
      e.Property(x => x.AuthId).IsRequired().HasMaxLength(64);
      e.Property(x => x.Title).IsRequired().HasMaxLength(500);
      e.Property(x => x.ActivityType).HasConversion<string>().HasMaxLength(64);
      e.HasIndex(x => x.AuthId);
      e.HasIndex(x => new { x.AuthId, x.CreatedAt });
    });

    modelBuilder.Entity<OtpCode>(e =>
    {
      e.ToTable("otp_codes");
      e.HasKey(x => x.Id);
      e.Property(x => x.Id).HasMaxLength(64);
      e.Property(x => x.UserId).IsRequired().HasMaxLength(64);
      e.Property(x => x.Code).IsRequired().HasMaxLength(128);
      e.Property(x => x.Type).HasConversion<string>().HasMaxLength(64);
      e.Property(x => x.Email).IsRequired().HasMaxLength(320);
      e.HasIndex(x => x.ExpiresAt);
      e.Ignore(x => x.IsValid);
    });

    modelBuilder.Entity<ProcessedWebhook>(e =>
    {
      e.ToTable("processed_webhooks");
      e.HasKey(x => x.Id);
      e.Property(x => x.Id).HasMaxLength(256);
    });

    modelBuilder.Entity<ProcessedServiceBusDelivery>(e =>
    {
      e.ToTable("processed_service_bus_deliveries");
      e.HasKey(x => x.Id);
      e.Property(x => x.Id).HasMaxLength(256);
    });
  }
}
