using ChepInlineApp.Helpers;
using HalconDotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Vision.Handlers.Core
{
    public class InspectionContext : IDisposable
    {
        private HImage? _image;
        public HImage? Image
        {
            get => _image;
            set => HalconDisposableHelper.DisposeAndAssign(ref _image, value);
        }
        public Dictionary<string, object> Parameters { get; } = new();
        public ConcurrentDictionary<string, object> InspectionResults { get; } = new();
        public string CameraId { get; set; } = string.Empty;

        public void Dispose()
        {
            _image?.Dispose();
            _image = null;
        }
    }
}
