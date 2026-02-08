using System;
using System.Collections.Generic;

namespace scilnst.Models;

public partial class Department
{
    public int DepartmentId { get; set; }

    public string FullName { get; set; } = null!;

    public string Floor { get; set; } = null!;

    public string ShortName { get; set; } = null!;

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
}
