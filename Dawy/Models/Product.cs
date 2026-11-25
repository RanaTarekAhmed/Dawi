

namespace Dawi.Models
{

    public class Product
    {
        public int ProductId { get; set; }
        public int DonorId { get; set; }
        public User Donor { get; set; } = null!;

        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateOnly ExpiryDate { get; set; }
        public string? ProductImageUrl { get; set; }
        public string Status { get; set; } = ProductStatus.Pending;
        public string? RejectionReason { get; set; }
        public int? PharmacyId { get; set; }
        public Pharmacy? Pharmacy { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<Request> Requests { get; set; } = new List<Request>();
    }
}


