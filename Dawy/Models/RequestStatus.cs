namespace Dawi.Models
{
    public static class RequestStatus
    {
        public const string Pending = "Pending";     // مستني موافقة الصيدلي
        public const string Approved = "Approved";    // اتوافق عليه، مستني الاستلام/الدفع
        public const string Rejected = "Rejected";    // الصيدلي رفض (RejectionReason)
        public const string Cancelled = "Cancelled";
        public const string Collected = "Collected";   // اتسلم فعليًا (CollectedAt)

   
    }
}


