using ChepInlineApp.Vision.Handlers.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Vision.Handlers.Interfaces
{
    public interface IInspectionRunner
    {
        Task<InspectionContext> RunAsync(InspectionContext context);
    }

}
