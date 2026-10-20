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

    [Phone]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "Contact number must be 11 digits.")]
    public string? ContactNumber { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? Age { get; set; }

    public bool IsIdVerified { get; set; }

    [ValidateNever]
    public virtual Status Status { get; set; } = null!;

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
