using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class Retail
{
    [Key]
    public int RetailId { get; set; }

    public string RetailName { get; set; } = null!;

    public int StatusId { get; set; }

    [Required(ErrorMessage = "Passcode is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "The passcode must be exactly 6 digits.")]
    public string Passcode { get; set; } = String.Empty;

    public string AdminOwner { get; set; } = null!;

    public string Address { get; set; } = null!;
    [ValidateNever]
    [ForeignKey(nameof(AdminOwner))]
    public virtual IdentityUser? AdminOwnerNavigation { get; set; } = null!;

    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

    public virtual ICollection<RetailStaff> RetailStaffs { get; set; } = new List<RetailStaff>();
    [ValidateNever]
    public virtual Status Status { get; set; } = null!;

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
