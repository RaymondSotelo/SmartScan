using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class Customer
{
    [Key]
    public int CustomerId { get; set; }

    public string FullName { get; set; } = null!;

    public string DigitalIdNumber { get; set; } = null!;

    public string FaceEmbedding { get; set; } = null!;

    public int StatusId { get; set; }

    public decimal CreditLimit { get; set; }

    public decimal CurrentDebt { get; set; }
    [ValidateNever]
    public virtual Status Status { get; set; } = null!;

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
