using System.ComponentModel.DataAnnotations;

namespace SmartScan.Models.ViewModels
{
    public class CustomerEnrollmentViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ContactNumber { get; set; }

        // Status indicators
        public bool HasFaceRegistered { get; set; }
        public bool HasDigitalIdRegistered { get; set; }

        // Form Inputs
        [Display(Name = "Digital ID / QR Payload")]
        public string? DigitalIdPayload { get; set; }

        // Serialized vector array generated on client by face model
        public string? FaceEmbedding { get; set; }
    }
}
