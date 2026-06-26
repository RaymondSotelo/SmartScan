using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class Product
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public int CategoryId { get; set; }

    public decimal Price { get; set; }

    public string ProductImage { get; set; } = null!;

    public int StatusId { get; set; }

    [ValidateNever]
    public virtual Category Category { get; set; } = null!;
    [ValidateNever]
    public virtual Status Status { get; set; } = null!;
}
