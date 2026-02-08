namespace scilnst.Models
{
    public class CurrentUser
    {
        public int EmployeeID { get; set; }
        public string FullName { get; set; }
        public int DepartmentID { get; set; }
        public string PositionTitle { get; set; }
        public bool IsGuest { get; set; }
        public string DepartmentShortName { get; set; }
    }
}