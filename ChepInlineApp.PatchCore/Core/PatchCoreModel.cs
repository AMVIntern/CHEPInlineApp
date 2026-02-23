using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using ChepInlineApp.PatchCore.Utils;

namespace ChepInlineApp.PatchCore.Core
{
    public sealed class PatchCoreModel : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly string _inputName;
        private readonly int _inputW;
        private readonly int _inputH;

        public PatchCoreModel(string modelPath, SessionOptions? options = null)
        {
            _session = ModelUtils.CreateSession(modelPath, options);
            var input = _session.InputMetadata.First();
            _inputName = input.Key;

            // Expect NCHW [1,3,H,W]
            var dims = input.Value.Dimensions;

            // dims could be int[] or IReadOnlyList<int> depending on package
            int rank = dims is Array a ? a.Length : dims.Count();

            int h = 448;
            int w = 448;

            if (rank >= 4)
            {
                // safe conversion
                h = Convert.ToInt32(dims[2] > 0 ? dims[2] : 448);
                w = Convert.ToInt32(dims[3] > 0 ? dims[3] : 448);
            }

            _inputH = h;
            _inputW = w;
        }

        public (float score, float mapMin, float mapMax, float mapMean, string scoreSource) Infer(Mat imageBgr, string mapScoreMode = "max")
        {
            using var resized = ImageUtils.ResizeWarp(imageBgr, _inputW, _inputH);
            var inputData = ImageUtils.ToRgbNchwFloat01(resized, _inputW, _inputH);

            var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 3, _inputH, _inputW });

            var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                };

            using var outputs = _session.Run(inputs);

            // 1) Try pick scalar float output
            foreach (var o in outputs)
            {
                if (o.Value is DenseTensor<float> tf && tf.Length <= 16)
                {
                    float v = tf.Buffer.Span[0];
                    // If it’s “binary-like”, skip (0/1 labels)
                    if (!(v == 0f || v == 1f))
                        return (v, float.NaN, float.NaN, float.NaN, o.Name);
                }

                if (o.Value is DenseTensor<double> td && td.Length <= 16)
                {
                    float v = (float)td.Buffer.Span[0];
                    if (!(v == 0f || v == 1f))
                        return (v, float.NaN, float.NaN, float.NaN, o.Name);
                }
            }

            // 2) Else pick anomaly map float output and reduce to score
            foreach (var o in outputs)
            {
                if (o.Value is DenseTensor<float> map && map.Rank >= 3)
                {
                    // Common shapes: [1,1,H,W] or [1,H,W]
                    float mn = float.PositiveInfinity;
                    float mx = float.NegativeInfinity;
                    double sum = 0;
                    long count = 0;

                    foreach (var v in map.Buffer.Span)
                    {
                        if (v < mn) mn = v;
                        if (v > mx) mx = v;
                        sum += v;
                        count++;
                    }
                    float mean = (count > 0) ? (float)(sum / count) : 0f;

                    float score = mapScoreMode.Equals("mean", StringComparison.OrdinalIgnoreCase) ? mean : mx;

                    return (score, mn, mx, mean, $"{o.Name}:{mapScoreMode}");
                }
            }

            throw new InvalidOperationException("PatchCore: No scalar score and no float anomaly_map output found.");
        }

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}