using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.MetadataExporter.Models
{
    public sealed class ImageCaptureRecord
    {
        public DateTime Timestamp { get; init; }
        public string ImagePath { get; init; } = string.Empty;
        public string Tag { get; init; } = string.Empty;
    }

}
