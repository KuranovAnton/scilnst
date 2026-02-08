using System;
using System.Collections.Generic;

namespace scilnst.Models;

public partial class Equipment
{
    public int EquipmentId { get; set; }

    public string Name { get; set; } = null!;

    public string InventoryNumber { get; set; } = null!;

    public decimal Weight { get; set; }

    public DateTime RegistrationDate { get; set; }

    public int? DepartmentId { get; set; }

    public int ServiceLifeYears { get; set; }

    public int? RoomId { get; set; }

    public bool IsArchived { get; set; }

    public string LocationType { get; set; } = null!;

    public string PhotoPath { get; set; } = null!;

    public string Description { get; set; } = null!;

    public virtual Department? Department { get; set; }

    public virtual Room? Room { get; set; }
}
