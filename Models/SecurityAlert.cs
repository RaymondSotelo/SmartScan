using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class SecurityAlert
{
    [Key]
    public int AlertId { get; set; }

    public int TransactionId { get; set; }

    public int AlertTypeId { get; set; }

    public string Description { get; set; } = null!;

    public int RetailStaffId { get; set; }

    public DateOnly CreatedAt { get; set; }
    [ValidateNever]
    public virtual AlertType AlertType { get; set; } = null!;
    
    [ValidateNever]
    public virtual RetailStaff RetailStaff { get; set; } = null!;
   [ValidateNever]
    public virtual Transaction Transaction { get; set; } = null!;
}
