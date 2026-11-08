

namespace Dawy.Models
{

    public class Product
    {
        public int ProductId { get; set; }
        public int DonorId { get; set; }
        public User Donor { get; set; } = null!;

        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; }
        public int Quantity { get; set; }
        public DateOnly ExpiryDate { get; set; }
        public string? ProductImageUrl { get; set; }
        public string Status { get; set; }

        public int? PharmacyId { get; set; }
        public bool IsControlled { get; set; }
        public bool RequiresColdChain { get; set; }
        public DateTime? InspectedAt { get; set; }
        public string? InspectionNotes { get; set; }
        public string? RejectionReason { get; set; }
        public Pharmacy? Pharmacy { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<Request> Requests { get; set; } = new List<Request>();
    }
}
