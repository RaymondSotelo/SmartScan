using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartScan.Models;

public partial class Category
{
    [Key]
    [Column("categoryId")]
    public int CategoryId { get; set; }

    [Column("categoryName")]
    public string CategoryName { get; set; } = null!;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
