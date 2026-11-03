using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class SecurityAlert
{
    [Key]
    public int AlertId { get; set; }

    public int AlertTypeId { get; set; }

    public string Description { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int? RetailId { get; set; }

    public string? ActionUrl { get; set; }

    [ValidateNever]
    public virtual AlertType AlertType { get; set; } = null!;
    
    [ValidateNever]
    public virtual Retail? Retail { get; set; }

}
