using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class AdminAlertRead
{
    [Key]
    public int AdminReadId { get; set; }

    [Required]
    public int AlertId { get; set; }

    [Required]
    [StringLength(450)]
    public string AdminId { get; set; } = null!;

    public bool IsRead { get; set; }

    [ValidateNever]
    [ForeignKey(nameof(AlertId))]
    public virtual SecurityAlert SecurityAlert { get; set; } = null!;

    [ValidateNever]
    [ForeignKey(nameof(AdminId))]
    public virtual IdentityUser Admin { get; set; } = null!;
}