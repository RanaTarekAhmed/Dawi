using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Dawi.Services
{
    public static class DocumentUploader
    {
        public const long MaxFileSizeBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };

        public static async Task<(string? Url, string? Error)> SaveAsync(
            IFormFile? file,
            IWebHostEnvironment environment,
            string folder,
            string fieldLabel)
        {
            if (file == null || file.Length == 0)
                return (null, $"{fieldLabel} مطلوب.");

            if (file.Length > MaxFileSizeBytes)
                return (null, $"حجم {fieldLabel} يجب ألا يتجاوز 5 ميجابايت.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                return (null, $"صيغة {fieldLabel} غير مدعومة. استخدم PDF أو JPG أو PNG.");

            var webRoot = string.IsNullOrWhiteSpace(environment.WebRootPath)
                ? Path.Combine(environment.ContentRootPath, "wwwroot")
                : environment.WebRootPath;

            var uploadsFolder = Path.Combine(webRoot, "uploads", folder);
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return ("/uploads/" + folder + "/" + fileName, null);
        }
    }
}
