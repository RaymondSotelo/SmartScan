using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class Barcode
{
    [Key]
    public int BarcodeId { get; set; }

    public string BarcodeLine { get; set; } = null!;

    public int InventoryId { get; set; }

    [ValidateNever]
    public virtual Inventory Inventory { get; set; } = null!;
}
