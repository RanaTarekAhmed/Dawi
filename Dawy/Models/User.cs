
using System.ComponentModel.DataAnnotations;
namespace Dawy.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = string.Empty;


        [RegularExpression(@"^\d{14}$", ErrorMessage = "الرقم القومي لازم يكون 14 رقم")]
        public string NationalId { get; set; } = string.Empty;  
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Product> DonatedProducts { get; set; } = new List<Product>();
        public ICollection<Request> Requests { get; set; } = new List<Request>();
        public ICollection<Pharmacy> ManagedPharmacies { get; set; } = new List<Pharmacy>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}
