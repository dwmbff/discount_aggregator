using AggStudentDiscounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AggStudentDiscounts.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Application> Applications { get; set; }
    public DbSet<Vote> Votes { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.Property(u => u.Nickname).IsRequired().HasMaxLength(100);
            entity.Property(u => u.University).HasMaxLength(200);
            entity.Property(u => u.Department).HasMaxLength(200);
            entity.Property(u => u.Role).HasMaxLength(50);
        });

        modelBuilder.Entity<Application>(entity =>
        {
            entity.HasIndex(a => a.Status);

            entity.HasOne(a => a.User)
                  .WithMany()
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(a => a.EstablishmentName).IsRequired().HasMaxLength(300);
            entity.Property(a => a.Address).IsRequired().HasMaxLength(500);
            entity.Property(a => a.DiscountDescription).IsRequired().HasMaxLength(1000);
            entity.Property(a => a.Conditions).IsRequired().HasMaxLength(1000);
            entity.Property(a => a.RejectionReason).HasMaxLength(500);

            entity.Property(a => a.Latitude).HasPrecision(18, 6);
            entity.Property(a => a.Longitude).HasPrecision(18, 6);
        });

        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasIndex(v => new { v.ApplicationId, v.UserId, v.CreatedAt });

            entity.HasOne(v => v.Application)
                  .WithMany()
                  .HasForeignKey(v => v.ApplicationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(v => v.User)
                  .WithMany()
                  .HasForeignKey(v => v.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}