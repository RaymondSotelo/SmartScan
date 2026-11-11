using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class Customer
{
    [Key]
    public int CustomerId { get; set; }

    public string FullName { get; set; } = null!;

    public string? FaceEmbedding { get; set; }

    public int StatusId { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal CreditLimit { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal CurrentDebt { get; set; }

    [Required(ErrorMessage = "Mobile contact number is required.")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Please enter a valid 11-digit mobile number starting with 09.")]
    [StringLength(11)]
    public string? ContactNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Required]
    public int RetailId { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    [ValidateNever]
    public virtual Status Status { get; set; } = null!;

    [ValidateNever]
    public virtual Retail? Retail { get; set; }

}
