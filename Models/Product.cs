using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class Product
{
    [Key]
    public int ProductId { get; set; }

    [Required]
    [Display(Name = "Product Name")]
    public string ProductName { get; set; } = null!;

    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public string ProductImage { get; set; } = null!;

    [Display(Name = "Status")]
    public int StatusId { get; set; }

    [Display(Name = "AI Scannable")]
    public bool CanScan { get; set; }

    [NotMapped]
    [Display(Name = "Upload Image")]
    public IFormFile? ImageUpload { get; set; }

    [ValidateNever]
    public virtual Category Category { get; set; } = null!;

    [ValidateNever]
    public virtual Status Status { get; set; } = null!;

    [ValidateNever]
    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

    [ValidateNever]
    public virtual ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
}