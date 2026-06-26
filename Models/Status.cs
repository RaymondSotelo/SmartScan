using System;
using System.Collections.Generic;

namespace SmartScan.Models;

public partial class Status
{
    public int StatusId { get; set; }

    public string StatusName { get; set; } = null!;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
