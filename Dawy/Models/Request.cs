

namespace Dawi.Models
{
    public class Request
    {
        public int RequestId { get; set; }

        public int BeneficiaryId { get; set; }
        public User Beneficiary { get; set; } = null!;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public string? PrescriptionImageUrl { get; set; }

        public string Status { get; set; } = RequestStatus.Pending;
        public DateTime RequestDate { get; set; }
        public int RequestedQuantity { get; set; }
        public DateTime? CollectionDate { get; set; }
        public decimal FeeAmount { get; set; }
        public bool IsPaid { get; set; }
        public string? RejectionReason { get; set; }

        public DateTime? CollectedAt { get; set; }   // وقت الاستلام الفعلي
    }
}


