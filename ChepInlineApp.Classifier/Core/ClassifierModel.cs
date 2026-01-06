using System.Diagnostics;
using ChepInlineApp.Classifier.Models;
using ChepInlineApp.Classifier.Utils;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace ChepInlineApp.Classifier.Core
{
    public class ClassifierModel : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly string _inputName;
        private readonly string _outputName;
        private readonly int[] _inputDims;
        private readonly int[] _outputDims;
        private readonly int _inputWidth;
        private readonly int _inputHeight;

        public int NumClasses { get; }

        private ClassifierPostProcessor _postProcess;

        public ClassifierModel(
            string modelPath,
            SessionOptions options = null)
        {
            _session = ModelUtils.CreateSession(modelPath, options);
            (_inputName, _outputName, _inputDims, _outputDims) = ModelUtils.GetModelMetadata(_session);
            _inputDims[0] = 1;
            _outputDims[0] = 1;
            _inputHeight = _inputDims[2];
            _inputWidth = _inputDims[3];
            NumClasses = _outputDims[1];

            // Create class labels: Class 0 = Pass, Classes 1, 2, 3... = Fail
            string[] classLabels = new string[NumClasses];
            for (int i = 0; i < NumClasses; i++)
            {
                if (i == 0)
                {
                    classLabels[i] = "Pass";
                }
                else
                {
                    classLabels[i] = $"Fail_{i}";
                }
            }

            _postProcess = new ClassifierPostProcessor(classLabels);
        }

        public ClassificationObject Infer(Mat imageBGR)
        {
            using var resized = ImageUtils.ResizeWarp(imageBGR, _inputWidth, _inputHeight);

            float[] inputTensorValues = ImageUtils.NormalizeToTensorPostResized(resized, _inputWidth, _inputHeight);

            // Step 1: Define shapes
            long[] inputShape = _inputDims.Select(d => (long)d).ToArray();
            long[] outputShape = _outputDims.Select(d => (long)d).ToArray();

            // Step 2: Allocate output buffer
            float[] outputTensorValues = new float[_outputDims.Aggregate(1, (a, b) => a * b)];

            // Step 3: Create memory info safely
            var memoryInfo = new OrtMemoryInfo(
                OrtMemoryInfo.allocatorCPU,
                OrtAllocatorType.ArenaAllocator,
                0,
                OrtMemType.Default
            );

            // Step 4: Wrap tensors
            using var inputOrt = OrtValue.CreateTensorValueFromMemory<float>(memoryInfo, inputTensorValues, inputShape);
            using var outputOrt = OrtValue.CreateTensorValueFromMemory<float>(memoryInfo, outputTensorValues, outputShape);

            Stopwatch timer = new Stopwatch();
            timer.Start();
            // Step 5: Run inference
            _session.Run(
                null,
                new[] { _inputName },
                new[] { inputOrt },
                new[] { _outputName },
                new[] { outputOrt }
            );
            timer.Stop();
            Debug.WriteLine($"Inference Time: {timer.ElapsedMilliseconds}");

            return _postProcess.Process(outputOrt.GetTensorDataAsSpan<float>().ToArray());
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

}
