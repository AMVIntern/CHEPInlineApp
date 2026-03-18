using Microsoft.ML.OnnxRuntime;
using System.Diagnostics;

namespace ChepInlineApp.YOLOX.Utils;

public static class ModelUtils
{
	public static SessionOptions GetDefaultSessionOptions()
	{
		var options = new SessionOptions
		{
			GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
		};

		try
		{
			if (IsCudaAvailable())
			{
				Debug.WriteLine("Using CUDAExecutionProvider");
				options.AppendExecutionProvider_CUDA(0);
			}
			else
			{
				Debug.WriteLine("CUDA not available, using CPUExecutionProvider");
			}
		}
		catch (Exception)
		{
		}

		return options;
	}

	public static InferenceSession CreateSession(string modelPath, SessionOptions options = null)
	{
		options ??= GetDefaultSessionOptions();

		if (!System.IO.File.Exists(modelPath))
			throw new ArgumentException($"Model path does not exist: {modelPath}");

		return new InferenceSession(modelPath, options);
	}

	public static (string inputName, string outputName, int[] inputDims, int[] outputDims)
		GetModelMetadata(InferenceSession session)
	{
		string inputName = session.InputMetadata.Keys.First();
		string outputName = session.OutputMetadata.Keys.First();
		int[] inputDims = session.InputMetadata[inputName].Dimensions;
		int[] outputDims = session.OutputMetadata[outputName].Dimensions;

		return (inputName, outputName, inputDims, outputDims);
	}

	public static bool IsCudaAvailable()
	{
		try
		{
			var providers = OrtEnv.Instance().GetAvailableProviders();
			return providers.Contains("CUDAExecutionProvider");
		}
		catch
		{
			return false;
		}
	}
}
