using ChepInlineApp.YOLOX.Models;
using OpenCvSharp;

namespace ChepInlineApp.YOLOX.Core;

public class YoloXPostProcessor
{
	private readonly int _numClasses;
	private readonly float _confThreshold;
	private readonly float _nmsThreshold;

	public YoloXPostProcessor(int numClasses, float conf = 0.5f, float nms = 0.5f)
	{
		_numClasses = numClasses;
		_confThreshold = conf;
		_nmsThreshold = nms;
	}

	public List<PredictionObject> DecodeOutput(
		float[] tensorOutput,
		float scale,
		int imgOriginalWidth,
		int imgOriginalHeight,
		int imageResizedWidth,
		int imageResizedHeight)
	{
		var predictionObjects = new List<PredictionObject>();
		var finalPredictions = new List<PredictionObject>();
		var strides = new List<int> { 8, 16, 32 };

		var gridStrides = GenerateGridsAndStrides(imageResizedWidth, imageResizedHeight, strides);
		GenerateYoloXProposals(gridStrides, tensorOutput, _confThreshold, _numClasses, ref predictionObjects);
		SortPredictionObjectsByConfidence(ref predictionObjects);

		List<int> pickedIndices = new List<int>();
		NonMaxSuppression(ref predictionObjects, ref pickedIndices, _nmsThreshold);

		int finalPredictionCount = pickedIndices.Count;
		finalPredictions.Capacity = finalPredictionCount;

		for (int i = 0; i < finalPredictionCount; i++)
		{
			var selectedPrediction = predictionObjects[pickedIndices[i]];

			float x0 = selectedPrediction.Rect.Left / scale;
			float y0 = selectedPrediction.Rect.Top / scale;
			float x1 = (selectedPrediction.Rect.Left + selectedPrediction.Rect.Width) / scale;
			float y1 = (selectedPrediction.Rect.Top + selectedPrediction.Rect.Height) / scale;

			x0 = MathF.Max(MathF.Min(x0, imgOriginalWidth - 1), 0f);
			y0 = MathF.Max(MathF.Min(y0, imgOriginalHeight - 1), 0f);
			x1 = MathF.Max(MathF.Min(x1, imgOriginalWidth - 1), 0f);
			y1 = MathF.Max(MathF.Min(y1, imgOriginalHeight - 1), 0f);

			finalPredictions.Add(new PredictionObject
			{
				Rect = new Rect2f(x0, y0, x1 - x0, y1 - y0),
				Label = selectedPrediction.Label,
				Probability = selectedPrediction.Probability
			});
		}

		return finalPredictions;
	}

	public List<GridAndStride> GenerateGridsAndStrides(int targetWidth, int targetHeight, List<int> strides)
	{
		var gridStrides = new List<GridAndStride>();

		foreach (var stride in strides)
		{
			int numGridW = targetWidth / stride;
			int numGridH = targetHeight / stride;

			for (int gh = 0; gh < numGridH; gh++)
			{
				for (int gw = 0; gw < numGridW; gw++)
				{
					gridStrides.Add(new GridAndStride { GridW = gw, GridH = gh, Stride = stride });
				}
			}
		}

		return gridStrides;
	}

	public void GenerateYoloXProposals(
		List<GridAndStride> gridStrides,
		float[] tensorOutput,
		float minConfidence,
		int numClasses,
		ref List<PredictionObject> predictionObjects)
	{
		predictionObjects.Clear();
		int num_grid_points = gridStrides.Count;

		for (int grid_index = 0; grid_index < num_grid_points; grid_index++)
		{
			int tensor_index = grid_index * (numClasses + 5);
			if ((tensor_index + 5 + numClasses) > tensorOutput.Length)
				continue;

			int gridW = gridStrides[grid_index].GridW;
			int gridH = gridStrides[grid_index].GridH;
			int stride = gridStrides[grid_index].Stride;

			float x_center = (tensorOutput[tensor_index + 0] + gridW) * stride;
			float y_center = (tensorOutput[tensor_index + 1] + gridH) * stride;
			float w = MathF.Exp(tensorOutput[tensor_index + 2]) * stride;
			float h = MathF.Exp(tensorOutput[tensor_index + 3]) * stride;
			float x0 = x_center - (w * 0.5f);
			float y0 = y_center - (h * 0.5f);

			float bbox_objectness = tensorOutput[tensor_index + 4];

			for (int class_index = 0; class_index < numClasses; class_index++)
			{
				float box_class_score = tensorOutput[tensor_index + 5 + class_index];
				float box_prob = bbox_objectness * box_class_score;

				if (box_prob > minConfidence)
				{
					predictionObjects.Add(new PredictionObject
					{
						Rect = new Rect2f(x0, y0, w, h),
						Label = class_index,
						Probability = box_prob
					});
				}
			}
		}
	}

	public void SortPredictionObjectsByConfidence(ref List<PredictionObject> predictionObjects)
	{
		predictionObjects.Sort((a, b) => b.Probability.CompareTo(a.Probability));
	}

	public void NonMaxSuppression(
		ref List<PredictionObject> predictionObject,
		ref List<int> pickedIndices,
		float nmsThreshold)
	{
		pickedIndices.Clear();
		int numPredictions = predictionObject.Count;
		List<float> areas = new List<float>(new float[numPredictions]);

		for (int i = 0; i < numPredictions; i++)
			areas[i] = predictionObject[i].Rect.Width * predictionObject[i].Rect.Height;

		for (int i = 0; i < numPredictions; i++)
		{
			var currentBox = predictionObject[i];
			bool keep = true;

			foreach (var pickedIndex in pickedIndices)
			{
				var pickedBox = predictionObject[pickedIndex];
				var intersection = currentBox.Rect.Intersect(pickedBox.Rect);
				float intersectionArea = intersection.Width * intersection.Height;
				float unionArea = areas[i] + areas[pickedIndex] - intersectionArea;
				float iou = unionArea > 0 ? intersectionArea / unionArea : 0;

				if (iou > nmsThreshold)
				{
					keep = false;
					break;
				}
			}

			if (keep)
				pickedIndices.Add(i);
		}
	}
}
