using ChepInlineApp.YOLOX.Models;
using ChepInlineApp.YOLOX.Utils;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChepInlineApp.YOLOX.Core;

public class YoloXModel : IDisposable
{
	private readonly InferenceSession _session;
	private readonly string _inputName;
	private readonly string _outputName;
	private readonly int[] _inputDims;
	private readonly int[] _outputDims;
	private readonly int _inputWidth;
	private readonly int _inputHeight;
	private readonly YoloXPostProcessor _postProcessor;

	public int NumClasses { get; }

	public YoloXModel(
		string modelPath,
		float confThreshold = 0.5f,
		float nmsThreshold = 0.5f,
		SessionOptions options = null)
	{
		_session = ModelUtils.CreateSession(modelPath, options);
		(_inputName, _outputName, _inputDims, _outputDims) = ModelUtils.GetModelMetadata(_session);
		_inputHeight = _inputDims[2];
		_inputWidth = _inputDims[3];
		NumClasses = _outputDims[2] - 5;

		_postProcessor = new YoloXPostProcessor(NumClasses, confThreshold, nmsThreshold);
	}

	public List<PredictionObject> Infer(Mat imageBGR)
	{
		using var resized = ImageUtils.static_resize(imageBGR, _inputWidth, _inputHeight);

		using var blob = CvDnn.BlobFromImage(
			resized,
			scaleFactor: 1.0,
			size: new OpenCvSharp.Size(),
			mean: new Scalar(0, 0, 0),
			swapRB: false,
			crop: false
		);

		float[] inputTensorValues = blob.AsSpan<float>().ToArray();
		Marshal.Copy(blob.Data, inputTensorValues, 0, inputTensorValues.Length);

		long[] inputShape = _inputDims.Select(d => (long)d).ToArray();
		long[] outputShape = _outputDims.Select(d => (long)d).ToArray();

		float[] outputTensorValues = new float[_outputDims.Aggregate(1, (a, b) => a * b)];

		var memoryInfo = new OrtMemoryInfo(
			OrtMemoryInfo.allocatorCPU,
			OrtAllocatorType.ArenaAllocator,
			0,
			OrtMemType.Default
		);

		using var inputOrt = OrtValue.CreateTensorValueFromMemory<float>(memoryInfo, inputTensorValues, inputShape);
		using var outputOrt = OrtValue.CreateTensorValueFromMemory<float>(memoryInfo, outputTensorValues, outputShape);

		var timer = Stopwatch.StartNew();
		_session.Run(
			null,
			new[] { _inputName },
			new[] { inputOrt },
			new[] { _outputName },
			new[] { outputOrt }
		);
		timer.Stop();
		Debug.WriteLine($"YOLOX Inference Time: {timer.ElapsedMilliseconds} ms");

		float scale = Math.Min((float)_inputWidth / imageBGR.Width, (float)_inputHeight / imageBGR.Height);

		return _postProcessor.DecodeOutput(
			outputOrt.GetTensorDataAsSpan<float>().ToArray(),
			scale,
			imageBGR.Width,
			imageBGR.Height,
			_inputWidth,
			_inputHeight
		);
	}

	public void Dispose()
	{
		_session?.Dispose();
	}
}
