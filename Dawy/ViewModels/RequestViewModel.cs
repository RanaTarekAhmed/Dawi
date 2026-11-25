using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Dawi.ViewModels
{
    public class RequestViewModel
    {
        [Required]
        public int ProductId { get; set; }

        // للعرض فقط في الصفحة
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int AvailableQuantity { get; set; }

        [Range(1, 1000, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "صورة الروشتة")]
        public IFormFile? PrescriptionImage { get; set; }

        [Url(ErrorMessage = "The link is not valid.")]
        public string? PrescriptionImageUrl { get; set; }
    }
}