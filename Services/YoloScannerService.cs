using Microsoft.AspNetCore.Http;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;

namespace SmartScan.Services
{
    public class YoloScannerService
    {
        // Declare the session variable first
        private readonly InferenceSession _session;

        // Wrap the execution code inside the constructor method
        public YoloScannerService()
        {
            // The path to your ONNX model file
            string modelPath = @"PythonScanner\my_model_v2\my_model.onnx";

            // Load the model into the ONNX runtime engine
            _session = new InferenceSession(modelPath);
        }

        public object DetectObjects(string base64Image)
        {
            // Clean the Base64 string if it contains the HTML canvas prefix
            if (base64Image.Contains(","))
            {
                base64Image = base64Image.Split(",")[1];
            }

            // Conver Base64 String into a row byte array
            byte[] imageBytes = Convert.FromBase64String(base64Image);

            // =========================================================================
            // TODO: ONNX IMAGE PROCESSING GOES HERE
            // You will need to use ImageSharp to load 'imageBytes', resize it to match 
            // your YOLO model (e.g., 640x640), convert the pixels into a DenseTensor, 
            // and pass it to _session.Run().
            // =========================================================================

            return new List<object>();
        }
    }
}