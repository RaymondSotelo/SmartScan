using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class TransactionItem
{
    [Key]
    public int TransactionItemId { get; set; }

    public decimal UnitPrice { get; set; }

    public int TransactionId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }
    [ValidateNever]
    public virtual Product Product { get; set; } = null!;
    [ValidateNever]
    public virtual Transaction Transaction { get; set; } = null!;
}
