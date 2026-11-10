using Microsoft.EntityFrameworkCore;
using Dawy.Models;

namespace Dawy.Data
{
    public class DawiContext : DbContext
    {
        public DawiContext(DbContextOptions<DawiContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Pharmacy> Pharmacies { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- User ----------
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.UserId);

                entity.Property(u => u.FullName).IsRequired().HasMaxLength(150);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(200);
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.PhoneNumber).HasMaxLength(20);
                entity.Property(u => u.Role).IsRequired().HasMaxLength(30);
                entity.Property(u => u.NationalId).IsRequired().HasMaxLength(14);
                entity.Property(u => u.IsActive).IsRequired();

                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.NationalId).IsUnique();
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.NotificationId);
                entity.Property(n => n.Title).IsRequired().HasMaxLength(150);
                entity.Property(n => n.Message).IsRequired().HasMaxLength(1000);
                entity.Property(n => n.Link).HasMaxLength(500);
                entity.HasOne(n => n.User).WithMany(u => u.Notifications)
                    .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(a => a.AuditLogId);
                entity.Property(a => a.Action).IsRequired().HasMaxLength(100);
                entity.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
                entity.Property(a => a.EntityId).HasMaxLength(100);
                entity.Property(a => a.Details).HasMaxLength(2000);
                entity.HasOne(a => a.User).WithMany(u => u.AuditLogs)
                    .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(a => a.CreatedAt);
            });

            // ---------- Pharmacy ----------
            modelBuilder.Entity<Pharmacy>(entity =>
            {
                entity.HasKey(p => p.PharmacyId);

                entity.Property(p => p.PharmacyName).IsRequired().HasMaxLength(150);
                entity.Property(p => p.Address).IsRequired().HasMaxLength(300);
                entity.Property(p => p.PhoneNumber).IsRequired().HasMaxLength(20);

                // الصيدلي (User) -> صيدلياته
                entity.HasOne(p => p.Pharmacist)
                      .WithMany(u => u.ManagedPharmacies)
                      .HasForeignKey(p => p.PharmacistId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- Product ----------
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.ProductId);

                entity.Property(p => p.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(p => p.Category).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Status).IsRequired().HasMaxLength(30);
                entity.Property(p => p.ProductImageUrl).HasMaxLength(500);

                // المتبرع -> المنتجات اللي تبرع بيها
                entity.HasOne(p => p.Donor)
                      .WithMany(u => u.DonatedProducts)
                      .HasForeignKey(p => p.DonorId)
                      .OnDelete(DeleteBehavior.Restrict);

                // الصيدلية (اختياري)
                entity.HasOne(p => p.Pharmacy)
                      .WithMany()
                      .HasForeignKey(p => p.PharmacyId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- Request ----------
            modelBuilder.Entity<Request>(entity =>
            {
                entity.HasKey(r => r.RequestId);

                entity.Property(r => r.PrescriptionImageUrl).HasMaxLength(500);

                // المستفيد -> طلباته
                entity.HasOne(r => r.Beneficiary)
                      .WithMany(u => u.Requests)
                      .HasForeignKey(r => r.BeneficiaryId)
                      .OnDelete(DeleteBehavior.Restrict);

                // المنتج -> طلباته
                entity.HasOne(r => r.Product)
                      .WithMany(p => p.Requests)
                      .HasForeignKey(r => r.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
