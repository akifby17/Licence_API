using Microsoft.EntityFrameworkCore;
using LicenseAPI.Models;

namespace LicenseAPI.Data
{
    public class LicenseDbContext : DbContext
    {
        public DbSet<License> Licenses { get; set; }

        public LicenseDbContext(DbContextOptions<LicenseDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<License>(entity =>
            {
                entity.ToTable("MobileLicenses");
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Id)
                    .HasDefaultValueSql("NEWID()");

                entity.Property(e => e.LicensePrefix)
                    .IsRequired()
                    .HasMaxLength(64);

                entity.Property(e => e.LicenseKeyHash)
                    .IsRequired()
                    .HasMaxLength(256);

                entity.Property(e => e.Salt)
                    .IsRequired()
                    .HasMaxLength(64);

                entity.Property(e => e.CompanyName)
                    .HasMaxLength(256);

                entity.Property(e => e.AssignedDeviceId)
                    .HasMaxLength(128);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.Property(e => e.Status)
                    .HasDefaultValue(LicenseStatus.Active);

                // Index for performance
                entity.HasIndex(e => e.LicensePrefix)
                    .IsUnique();
                entity.HasIndex(e => e.AssignedDeviceId);
                entity.HasIndex(e => e.ExpiresAt);
                entity.HasIndex(e => e.Status);
            });
        }
    }
}
