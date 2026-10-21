using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmartScan.Services
{
    public class YoloScannerService
    {
        private readonly InferenceSession _session;

        private const float CONFIDENCE_THRESHOLD = 0.40f;
        private const int MODEL_INPUT_SIZE = 640;

        // Fallback array
        private string[] _classes = {
            "AJI-NO-MOTO",
            "Coca-Cola Swakto",
            "Kopiko Blanca",
            "Kopiko Brown",
            "Lady's Choice",
            "Magic Sarap",
            "Nescafe Stick"
        };

        public YoloScannerService()
        {
            var options = new Microsoft.ML.OnnxRuntime.SessionOptions();
            options.AppendExecutionProvider_CPU();
            _session = new InferenceSession(@"PythonScanner\my_model_v2\my_model.onnx", options);

            try
            {
                var meta = _session.ModelMetadata.CustomMetadataMap;
                if (meta.ContainsKey("names"))
                {
                    var rawNames = meta["names"].Trim('{', '}');
                    var pairs = rawNames.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                    var dict = new SortedDictionary<int, string>();
                    foreach (var pair in pairs)
                    {
                        var parts = pair.Split(new[] { ": " }, StringSplitOptions.None);
                        if (parts.Length == 2 && int.TryParse(parts[0].Trim('\'', '\"', ' '), out int idx))
                        {
                            dict[idx] = parts[1].Trim('\'', '\"', ' ');
                        }
                    }
                    if (dict.Count > 0) _classes = dict.Values.ToArray();
                }
            }
            catch { }

            for (int i = 0; i < _classes.Length; i++)
            {
                if (_classes[i].IndexOf("brown", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _classes[i] = "Kopiko Brown Twinpack";
                }
                else if (_classes[i].IndexOf("blanca", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _classes[i] = "Kopiko Blanca Twinpack";
                }
            }
        }

        public object DetectObjects(string base64Image, List<string> storeInventory)
        {
            if (base64Image.Contains(",")) base64Image = base64Image.Split(",")[1];

            byte[] imageBytes = Convert.FromBase64String(base64Image);

            using var originalImage = Image.Load<Rgb24>(imageBytes);
            int origW = originalImage.Width;
            int origH = originalImage.Height;

            float r = Math.Min((float)MODEL_INPUT_SIZE / origW, (float)MODEL_INPUT_SIZE / origH);
            int unpadW = (int)Math.Round(origW * r);
            int unpadH = (int)Math.Round(origH * r);
            int padX = (MODEL_INPUT_SIZE - unpadW) / 2;
            int padY = (MODEL_INPUT_SIZE - unpadH) / 2;

            using var resized = originalImage.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(unpadW, unpadH),
                Mode = ResizeMode.Stretch
            }));

            var tensor = new DenseTensor<float>(new[] { 1, 3, MODEL_INPUT_SIZE, MODEL_INPUT_SIZE });

            float fillVal = 114f / 255f;
            for (int ch = 0; ch < 3; ch++)
                for (int y = 0; y < MODEL_INPUT_SIZE; y++)
                    for (int x = 0; x < MODEL_INPUT_SIZE; x++)
                        tensor[0, ch, y, x] = fillVal;

            resized.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgb24> row = accessor.GetRowSpan(y);
                    int targetY = y + padY;
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        int targetX = x + padX;
                        ref Rgb24 px = ref row[x];
                        tensor[0, 0, targetY, targetX] = px.R / 255.0f;
                        tensor[0, 1, targetY, targetX] = px.G / 255.0f;
                        tensor[0, 2, targetY, targetX] = px.B / 255.0f;
                    }
                }
            });

            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", tensor) };
            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            int numClasses = _classes.Length;
            int numDetections = output.Dimensions[2];

            var candidates = new List<DetectionCandidate>();

            for (int i = 0; i < numDetections; i++)
            {
                float maxScore = 0f;
                int bestClass = -1;

                for (int c = 0; c < numClasses; c++)
                {
                    float score = output[0, 4 + c, i];
                    if (score > maxScore)
                    {
                        maxScore = score;
                        bestClass = c;
                    }
                }

                if (maxScore >= CONFIDENCE_THRESHOLD)
                {
                    string rawName = _classes[bestClass];
                    string dbName = rawName.ToLower().Replace(" ", "_").Replace("'", "''").Trim();

                    bool isAllowedInStore = storeInventory != null && storeInventory.Any(inv =>
                        inv.Trim().Equals(dbName, StringComparison.OrdinalIgnoreCase));

                    if (isAllowedInStore)
                    {
                        float cx = output[0, 0, i];
                        float cy = output[0, 1, i];
                        float w = output[0, 2, i];
                        float h = output[0, 3, i];

                        float x1 = ((cx - w / 2f) - padX) / r;
                        float y1 = ((cy - h / 2f) - padY) / r;
                        float x2 = ((cx + w / 2f) - padX) / r;
                        float y2 = ((cy + h / 2f) - padY) / r;

                        x1 = Math.Clamp(x1, 0, origW);
                        y1 = Math.Clamp(y1, 0, origH);
                        x2 = Math.Clamp(x2, 0, origW);
                        y2 = Math.Clamp(y2, 0, origH);

                        candidates.Add(new DetectionCandidate
                        {
                            Name = rawName,
                            Confidence = (int)Math.Round(maxScore * 100),
                            Box = new[] { (int)x1, (int)y1, (int)x2, (int)y2 }
                        });
                    }
                }
            }

            // 🔴 NON-MAXIMUM SUPPRESSION (NMS): Keeps duplicates if they are physically separate
            var sortedCandidates = candidates.OrderByDescending(c => c.Confidence).ToList();
            var finalCandidates = new List<DetectionCandidate>();

            foreach (var candidate in sortedCandidates)
            {
                bool keep = true;
                foreach (var kept in finalCandidates.Where(k => k.Name == candidate.Name))
                {
                    float iou = ComputeIoU(candidate.Box, kept.Box);
                    if (iou > 0.40f) // If boxes overlap > 40%, it's the same item. Otherwise, it's a new item.
                    {
                        keep = false;
                        break;
                    }
                }

                if (keep) finalCandidates.Add(candidate);
            }

            var finalDetections = finalCandidates.Select(d => new
            {
                name = d.Name,
                confidence = d.Confidence,
                is_confident = true,
                box = d.Box
            }).ToList();

            return finalDetections;
        }

        // Calculates Intersection over Union to tell boxes apart
        private float ComputeIoU(int[] box1, int[] box2)
        {
            int x1 = Math.Max(box1[0], box2[0]);
            int y1 = Math.Max(box1[1], box2[1]);
            int x2 = Math.Min(box1[2], box2[2]);
            int y2 = Math.Min(box1[3], box2[3]);

            int intersectionArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            int box1Area = (box1[2] - box1[0]) * (box1[3] - box1[1]);
            int box2Area = (box2[2] - box2[0]) * (box2[3] - box2[1]);

            int unionArea = box1Area + box2Area - intersectionArea;
            return unionArea == 0 ? 0 : (float)intersectionArea / unionArea;
        }

        private class DetectionCandidate
        {
            public string Name { get; set; } = string.Empty;
            public int Confidence { get; set; }
            public int[] Box { get; set; } = Array.Empty<int>();
        }
    }
}