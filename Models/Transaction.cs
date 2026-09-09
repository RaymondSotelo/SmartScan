using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class Transaction
{
    [Key]
    public int TransactionId { get; set; }

    public int? RetailStaffId { get; set; }

    public int? CustomerId { get; set; }

    public int? RetailId { get; set; }

    public decimal? TotalAmount { get; set; }

    public int? PaymentMethodId { get; set; }

    public int? StatusId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? ReceiptNumber { get; set; }
    [ValidateNever]
    public virtual Customer? Customer { get; set; }
    [ValidateNever]
    public virtual PaymentMethod? PaymentMethod { get; set; }
    [ValidateNever]
    public virtual Retail? Retail { get; set; }
    [ValidateNever]
    public virtual RetailStaff? RetailStaff { get; set; }

    public virtual ICollection<SecurityAlert> SecurityAlerts { get; set; } = new List<SecurityAlert>();
    [ValidateNever]
    public virtual Status? Status { get; set; }

    public virtual ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
}
