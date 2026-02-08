using System;
using System.Collections.Generic;

namespace scilnst.Models;

public partial class Employee
{
    public string LastName { get; set; } = null!;

    public int EmployeeId { get; set; }

    public string FirstName { get; set; } = null!;

    public int DepartmentId { get; set; }

    public string? Username { get; set; }

    public string? MiddleName { get; set; }

    public int PositionId { get; set; }

    public int BirthYear { get; set; }

    public string? Password { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual JobPosition Position { get; set; } = null!;
}
