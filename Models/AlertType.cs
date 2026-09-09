using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartScan.Models;

public partial class AlertType
{
    [Key]
    public int AlertTypeId { get; set; }

    public string AlertTypeName { get; set; } = null!;

    public virtual ICollection<SecurityAlert> SecurityAlerts { get; set; } = new List<SecurityAlert>();
}
