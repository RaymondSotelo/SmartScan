using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class Inventory
{
    [Key]
    public int InventoryId { get; set; }

    public int ProductId { get; set; }

    public decimal LocalPrice { get; set; }

    public int Stock { get; set; }

    public string? Barcode { get; set; }

    public int RetailId { get; set; }
    [ValidateNever]
    public virtual Product Product { get; set; } = null!;
    [ValidateNever]
    public virtual Retail Retail { get; set; } = null!;
}
