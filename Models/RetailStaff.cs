using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class RetailStaff
{
    [Key]
    public int RetailStaffId { get; set; }

    public string CashierId { get; set; } = null!;

    public int StatusId { get; set; }

    public int RetailId { get; set; }

    [ValidateNever]
    [ForeignKey(nameof(CashierId))]
    public virtual IdentityUser? Cashier { get; set; } = null!;

    [ValidateNever]
    public virtual Retail Retail { get; set; } = null!;

    public virtual ICollection<SecurityAlert> SecurityAlerts { get; set; } = new List<SecurityAlert>();

    [ValidateNever]
    public virtual Status Status { get; set; } = null!;

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
