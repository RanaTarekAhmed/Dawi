namespace Dawy.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; }
        public string NationalId { get; set; } = string.Empty;   // يُفضل تشفيره
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
