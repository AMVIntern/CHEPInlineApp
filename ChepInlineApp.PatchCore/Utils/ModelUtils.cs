using Microsoft.ML.OnnxRuntime;

namespace ChepInlineApp.PatchCore.Utils
{
    public static class ModelUtils
    {
        public static SessionOptions GetDefaultSessionOptions()
        {
            var so = new SessionOptions();

            // Keep minimal + safe defaults
            so.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            so.EnableMemoryPattern = true;
            so.EnableCpuMemArena = true;

            return so;
        }

        public static InferenceSession CreateSession(string modelPath, SessionOptions? options = null)
        {
            options ??= GetDefaultSessionOptions();
            return new InferenceSession(modelPath, options);
        }
    }
}