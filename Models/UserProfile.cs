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

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? ProfilePicture { get; set; } = null!;

    public string UserId { get; set; } = null!;

    [ValidateNever]
    [ForeignKey(nameof(UserId))]
    public virtual IdentityUser? User { get; set; } = null!;
}
