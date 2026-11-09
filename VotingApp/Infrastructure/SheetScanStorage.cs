using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace VotingApp.Infrastructure
{
    /// <summary>
    /// Stores photos/scans of signed paper result sheets under wwwroot/uploads/sheets. They are published,
    /// so the content type is decided from the file's own signature, never from the client's name or header.
    /// </summary>
    public class SheetScanStorage
    {
        public const long MaxBytes = 8 * 1024 * 1024;
        private readonly IWebHostEnvironment environment;

        public SheetScanStorage(IWebHostEnvironment environment) => this.environment = environment;

        /// <summary>Returns the extension implied by the file signature (".jpg", ".png", ".pdf"), or null if not allowed.</summary>
        public static async Task<string> DetectAsync(IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length < 8 || file.Length > MaxBytes) return null;
            var header = new byte[8];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
            if (read < 4) return null;
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return ".jpg";
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return ".png";
            if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46) return ".pdf"; // %PDF
            return null;
        }

        /// <summary>Saves the file and returns its web path (e.g. "/uploads/sheets/abc.jpg").</summary>
        public async Task<string> SaveAsync(IFormFile file, string extension, CancellationToken ct = default)
        {
            var folder = Path.Combine(environment.WebRootPath, "uploads", "sheets");
            Directory.CreateDirectory(folder);
            var name = Guid.NewGuid().ToString("N") + extension;
            await using var output = new FileStream(Path.Combine(folder, name), FileMode.CreateNew);
            await file.CopyToAsync(output, ct);
            return "/uploads/sheets/" + name;
        }
    }
}
