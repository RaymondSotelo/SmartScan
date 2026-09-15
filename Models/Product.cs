using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class Product
{
    [Key]
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public int CategoryId { get; set; }
    [Required(ErrorMessage = "Local price is required.")]
    [Range(0.01, 999999.99, ErrorMessage = "Price must be a positive number greater than 0.")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public string ProductImage { get; set; } = null!;

    public int StatusId { get; set; }
    [ValidateNever]
    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    [ValidateNever]
    public virtual Status Status { get; set; } = null!;

    public virtual ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
}
