using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

#nullable enable

namespace Textgen
{
    public class ImageAttachment
    {
        private static readonly Dictionary<string, string> MimeTypeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".bmp"] = "image/bmp",
            [".tiff"] = "image/tiff",
            [".tif"] = "image/tiff",
            [".heic"] = "image/heic"
        };

        public string? FileName { get; set; }
        public string? SourcePath { get; set; }
        public string? StoredPath { get; set; }
        public string? MimeType { get; set; }
        public long? SizeBytes { get; set; }
        public string? Sha256 { get; set; }
        public string? Caption { get; set; }

        public static bool IsSupportedExtension(string path)
        {
            var extension = Path.GetExtension(path) ?? string.Empty;
            return MimeTypeMap.ContainsKey(extension);
        }

        public static ImageAttachment FromFile(string filePath, string? storedPath = null, string? caption = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            var fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Attachment file not found.", fullPath);
            }

            var fileInfo = new FileInfo(fullPath);
            var extension = fileInfo.Extension ?? string.Empty;
            var shaValue = ComputeSha256(fullPath);

            return new ImageAttachment
            {
                FileName = fileInfo.Name,
                SourcePath = fullPath,
                StoredPath = storedPath ?? fileInfo.Name,
                MimeType = GetMimeType(extension),
                SizeBytes = fileInfo.Length,
                Sha256 = shaValue,
                Caption = caption
            };
        }

        public ImageAttachment Clone()
        {
            return new ImageAttachment
            {
                FileName = FileName,
                SourcePath = SourcePath,
                StoredPath = StoredPath,
                MimeType = MimeType,
                SizeBytes = SizeBytes,
                Sha256 = Sha256,
                Caption = Caption
            };
        }

        private static string? GetMimeType(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return null;
            }

            if (MimeTypeMap.TryGetValue(extension, out var mime))
            {
                return mime;
            }

            return "application/octet-stream";
        }

        private static string ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
