

namespace Dawy.Models
{
    public class Request
    {
        public int RequestId { get; set; }

        public int BeneficiaryId { get; set; }
        public User Beneficiary { get; set; } = null!;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public string? PrescriptionImageUrl { get; set; }

        public DateTime RequestDate { get; set; }
    }
}
