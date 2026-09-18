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

    public string? DigitalIdNumber { get; set; }

    public string? FaceEmbedding { get; set; }

    public int StatusId { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal CreditLimit { get; set; } = 0.00m;

    [Column(TypeName = "decimal(10, 2)")]
    public decimal CurrentDebt { get; set; } = 0.00m;

    [Required(ErrorMessage = "Contact number is required.")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Contact number must be an 11-digit mobile number starting with 09.")] public string? ContactNumber { get; set; }

    public string? DigitalIdHash { get; set; }

    public bool HasFaceRegistered { get; set; }

    public DateTime CreatedAt { get; set; }

    [ValidateNever]
    public virtual Status Status { get; set; } = null!;

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

}
