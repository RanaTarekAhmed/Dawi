namespace Dawy.Models;

public class User
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public string NationalId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Product> DonatedProducts { get; set; } = new List<Product>();
    public ICollection<Request> Requests { get; set; } = new List<Request>();
    public ICollection<Pharmacy> ManagedPharmacies { get; set; } = new List<Pharmacy>();
}