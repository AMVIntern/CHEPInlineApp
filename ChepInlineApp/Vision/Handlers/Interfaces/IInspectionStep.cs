using ChepInlineApp.Vision.Handlers.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Vision.Handlers.Interfaces
{
    public interface IInspectionStep
    {
        string Name { get; }
        Task RunAsync(InspectionContext context);
    }
}
