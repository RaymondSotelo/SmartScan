using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartScan.Models;

public partial class PaymentMethod
{
    [Key]
    public int PaymentMethodsId { get; set; }

    public string PaymentMethodName { get; set; } = null!;

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
