using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class Inventory
{
    [Key]
    public int InventoryId { get; set; }

    public int ProductId { get; set; }

    [Required(ErrorMessage = "Local price is required.")]
    [Range(0.01, 999999.99, ErrorMessage = "Price must be a positive number greater than 0.")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal LocalPrice { get; set; }

    [Required(ErrorMessage = "Stock quantity is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "Stock must be a non-negative whole number.")]
    public int Stock { get; set; }

    public int? BarcodeId { get; set; }

    public int RetailId { get; set; }

    public virtual ICollection<Barcode> Barcodes { get; set; } = new List<Barcode>();

    [ValidateNever]
    public virtual Product Product { get; set; } = null!;
    [ValidateNever]
    public virtual Retail Retail { get; set; } = null!;
}
