namespace DhlLogistics.Shared.Models;

public class StaffDepartment
{
    public int Id { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
