using Dawi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dawi.Data
{
    public static class DbInitializer
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DawiContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

            context.Database.Migrate();

            if (!context.Users.Any(u => u.Email == "admin@dawi.com"))
            {
                context.Users.Add(CreateUser(passwordHasher, "مدير داوي", "admin@dawi.com", "01000000000", "00000000000000", Roles.Admin, "Admin@123"));
            }

            if (!context.Users.Any(u => u.Email == "donor.demo@dawi.com"))
            {
                context.Users.Add(CreateUser(passwordHasher, "متبرع تجريبي", "donor.demo@dawi.com", "01000000001", "11111111111111", Roles.Donor, "Demo@123"));
            }

            var pharmacist = context.Users.FirstOrDefault(u => u.Email == "pharmacist.demo@dawi.com");
            if (pharmacist == null)
            {
                pharmacist = CreateUser(passwordHasher, "صيدلي تجريبي", "pharmacist.demo@dawi.com", "01000000002", "22222222222222", Roles.Pharmacist, "Demo@123");
                context.Users.Add(pharmacist);
            }

            if (!context.Users.Any(u => u.Email == "beneficiary.demo@dawi.com"))
            {
                context.Users.Add(CreateUser(passwordHasher, "مستفيد تجريبي", "beneficiary.demo@dawi.com", "01000000003", "33333333333333", Roles.Beneficiary, "Demo@123"));
            }

            context.SaveChanges();

            var pharmacy = context.Pharmacies.FirstOrDefault(p => p.PharmacistId == pharmacist.UserId);
            if (pharmacy == null)
            {
                pharmacy = new Pharmacy
                {
                    PharmacyName = "صيدلية داوي التجريبية",
                    Address = "شارع النيل",
                    City = "القاهرة",
                    PhoneNumber = "01000000002",
                    PharmacistId = pharmacist.UserId,
                    IsVerified = true
                };
                context.Pharmacies.Add(pharmacy);
                context.SaveChanges();
            }

            if (!context.Products.Any())
            {
                var donor = context.Users.First(u => u.Email == "donor.demo@dawi.com");
                context.Products.AddRange(
                    new Product
                    {
                        DonorId = donor.UserId,
                        ProductName = "باراسيتامول",
                        Category = "أدوية",
                        Quantity = 20,
                        ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
                        Status = ProductStatus.Approved,
                        PharmacyId = pharmacy.PharmacyId,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Product
                    {
                        DonorId = donor.UserId,
                        ProductName = "جهاز قياس ضغط",
                        Category = "أجهزة طبية",
                        Quantity = 3,
                        ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(2)),
                        Status = ProductStatus.Approved,
                        PharmacyId = pharmacy.PharmacyId,
                        CreatedAt = DateTime.UtcNow
                    });
                context.SaveChanges();
            }
        }

        private static User CreateUser(IPasswordHasher<User> passwordHasher, string fullName, string email,
            string phone, string nationalId, string role, string password)
        {
            var user = new User
            {
                FullName = fullName,
                Email = email,
                PhoneNumber = phone,
                NationalId = nationalId,
                Role = role,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            user.PasswordHash = passwordHasher.HashPassword(user, password);
            return user;
        }
    }
}
