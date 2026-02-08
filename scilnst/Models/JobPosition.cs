using System;
using System.Collections.Generic;

namespace scilnst.Models;

public partial class JobPosition
{
    public int PositionId { get; set; }

    public decimal Salary { get; set; }

    public string Title { get; set; } = null!;

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
