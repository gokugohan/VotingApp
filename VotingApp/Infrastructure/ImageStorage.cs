using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace VotingApp.Infrastructure
{
    /// <summary>Validates and stores uploaded candidate images under wwwroot/images/candidates.</summary>
    public class ImageStorage
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        public const long MaxBytes = 2 * 1024 * 1024;
        private readonly IWebHostEnvironment environment;

        public ImageStorage(IWebHostEnvironment environment) => this.environment = environment;

        /// <summary>Returns an error message, or null if the file is an acceptable image. SVG is rejected (it can carry script).</summary>
        public static string Validate(IFormFile file)
        {
            if (file == null) return null;
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension) ||
                file.ContentType == null || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return "Only JPG, PNG, GIF or WEBP images are allowed.";
            }
            if (file.Length == 0 || file.Length > MaxBytes) return "The image must be smaller than 2 MB.";
            return null;
        }

        /// <summary>Saves a validated image under a server-generated name and returns that name.</summary>
        public async Task<string> SaveCandidateImageAsync(IFormFile file, CancellationToken ct = default)
        {
            if (file == null) return null;
            var folder = Path.Combine(environment.WebRootPath, "images", "candidates");
            Directory.CreateDirectory(folder);
            // Never reuse the client's file name: it can contain path segments or misleading extensions.
            var name = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
            await using var stream = new FileStream(Path.Combine(folder, name), FileMode.CreateNew);
            await file.CopyToAsync(stream, ct);
            return name;
        }
    }
}
