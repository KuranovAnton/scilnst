namespace scilnst.Models
{
        public class CurrentUser
        {
            public int EmployeeID { get; set; }
            public string FullName { get; set; } = string.Empty;
            public int? DepartmentID { get; set; }
            public string PositionTitle { get; set; } = string.Empty;
            public bool IsGuest { get; set; }
            public string DepartmentShortName { get; set; } = string.Empty;
        }
}