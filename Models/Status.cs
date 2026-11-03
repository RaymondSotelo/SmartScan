using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartScan.Models;

public partial class Status
{
    [Key]
    [Column("statusId")]
    public int StatusId { get; set; }

    [Column("statusName")]
    public string StatusName { get; set; } = null!;

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<RetailStaff> RetailStaffs { get; set; } = new List<RetailStaff>();

    public virtual ICollection<Retail> Retails { get; set; } = new List<Retail>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
