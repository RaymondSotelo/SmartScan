using System.Text.Json;

namespace SmartScan.Models.ViewModels
{
    public class SalesAnalysisViewModel
    {
        // KPI Metrics
        public int TodayTransactions { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalItemsSold { get; set; }
        public string TopSellingItemName { get; set; } = "N/A";

        // Data for Graph 1: RevenueLine Chart)
        public string CurrentRange { get; set; } = "7days";
        public string RangeLabel { get; set; } = "Last 7 Days";
        public string RevenueDatesJson { get; set; } = "[]";
        public string RevenueValuesJson { get; set; } = "[]";

        // Data for Graph 2: Top 5 Products (Bar Chart)
        public string TopProductNamesJson { get; set; } = "[]";
        public string TopProductQuantitiesJson { get; set; } = "[]";

        // Data for Graph 3: Payment Methods (Doughnut Chart)
        public string PaymentMethodLabelsJson { get; set; } = "[]";
        public string PaymentMethodCountsJson { get; set; } = "[]";
    }
}
