using System;

#nullable enable

namespace Textgen
{
    public sealed class FileAttachment : Attachment
    {
        public FileAttachment(string type) : base(type)
        {
        }

        public override Attachment Clone()
        {
            var clone = new FileAttachment(Type);
            CopyBasePropertiesTo(clone);
            return clone;
        }
    }
}
