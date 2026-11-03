using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;

namespace SmartScan.Models;

public partial class Product
{
    [Key]
    [Column("productId")]
    public int ProductId { get; set; }

    [Required]
    [Column("productName")]
    [Display(Name = "Product Name")]
    public string ProductName { get; set; } = null!;

    [Column("categoryId")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Required]
    [Column("price")]
    [Precision(18, 2)]
    public decimal Price { get; set; }

    [Column("productImage")]
    public string ProductImage { get; set; } = null!;

    [Display(Name = "Status")]
    [Column("statusId")]
    public int StatusId { get; set; }

    [Display(Name = "AI Scannable")]
    [Column("canScan")]
    public bool CanScan { get; set; }

    [Column("isAgeRestricted")]
    public bool IsAgeRestricted { get; set; }

    [Column("barcode")]
    public string? Barcode { get; set; }

    [NotMapped]
    [Column("imageUpload")]
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

