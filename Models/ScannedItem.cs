namespace SmartScan.Models
{
    public class ScannedItem
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public double Confidence { get; set; }
    }
}