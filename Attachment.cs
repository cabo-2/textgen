using System;

#nullable enable

namespace Textgen
{
    public abstract class Attachment
    {
        protected Attachment(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException("Attachment type cannot be null or empty.", nameof(type));
            }

            Type = type;
        }

        public string Type { get; }
        public string? FileName { get; set; }
        public string? SourcePath { get; set; }
        public string? StoredPath { get; set; }
        public string? MimeType { get; set; }
        public long? SizeBytes { get; set; }
        public string? Sha256 { get; set; }
        public string? Caption { get; set; }

        public abstract Attachment Clone();

        protected void CopyBasePropertiesTo(Attachment target)
        {
            target.FileName = FileName;
            target.SourcePath = SourcePath;
            target.StoredPath = StoredPath;
            target.MimeType = MimeType;
            target.SizeBytes = SizeBytes;
            target.Sha256 = Sha256;
            target.Caption = Caption;
        }
    }
}
