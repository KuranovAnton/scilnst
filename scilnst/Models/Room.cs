using System;
using System.Collections.Generic;

namespace scilnst.Models;

public partial class Room
{
    public int RoomId { get; set; }

    public int DepartmentId { get; set; }

    public string RoomNumber { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}
