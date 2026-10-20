using SmartScan.Models;

namespace SmartScan.Models.ViewModels
{
    public class DashboardViewModel
    {
        // Section 1: KPI Metrics
        public decimal TotalOutstandingDebt { get; set; }
        public decimal OverdueDebtAmount { get; set; }
        public decimal TodaysSales { get; set; }
        public int TotalCustomers { get; set; }
        public int VerifiedCustomersCount { get; set; }
        public int UnderageCustomersCount { get; set; }
        public int ActiveRetailCount { get; set; }
        public int TotalRetailCount { get; set; }

        // Section 2: Critical Attention Items
        public List<OverdueDebtItem> OverdueAccounts { get; set; } = new();
        public List<ComplianceNoticeItem> RecentVerifications { get; set; } = new();

        // Section 3: Retail Directory
        public IEnumerable<Retail> Retails { get; set; } = new List<Retail>();
        public Dictionary<string, string> ProfileMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string? CurrentFilter { get; set; }
    }

    public class OverdueDebtItem
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ContactNumber { get; set; }
        public decimal AmountDue { get; set; }
        public DateOnly DueDate { get; set; }

        // Calculates elapsed days between two DateOnly instances
        public int DaysOverdue => DateOnly.FromDateTime(DateTime.Today).DayNumber - DueDate.DayNumber;
    }

    public class ComplianceNoticeItem
    {
        public string CustomerMaskedName { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool IsUnderage { get; set; }
        public DateTime RegisteredAt { get; set; }
    }
}