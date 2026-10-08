using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartScan.Models;

public partial class UserProfile
{
    [Key]
    public int ProfileId { get; set; }
    [Required]
    [MaxLength(50)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = null!;
    [Required]
    [MaxLength(50)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = null!;
    [MaxLength(260)]
    public string? ProfilePicture { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public bool UseAsDisplayName { get; set; } = false;

    [ValidateNever]
    [ForeignKey(nameof(UserId))]
    public virtual IdentityUser? User { get; set; } = null!;
}
