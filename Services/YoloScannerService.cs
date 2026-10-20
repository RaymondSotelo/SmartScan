using Microsoft.AspNetCore.Http;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;

namespace SmartScan.Services
{
    public class YoloScannerService
    {
        // 1. Declare the session variable first
        private readonly InferenceSession _session;

        // 2. Wrap the execution code inside the constructor method
        public YoloScannerService()
        {
            // The path to your ONNX model file
            string modelPath = @"PythonScanner\my_model_v2\my_model.onnx";

            // Load the model into the ONNX runtime engine
            _session = new InferenceSession(modelPath);
        }
    }
}