using Dawy.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace Dawy.ViewModels
{
    public class ProductViewModel
    {
        [Required]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        [Range(1, 10000)]
        public int Quantity { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        [Required]
        public int PharmacyId { get; set; }
    }
}



  
      