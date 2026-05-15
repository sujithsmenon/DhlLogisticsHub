namespace DhlLogistics.Shared.Models;

public class StaffDesignation
{
    public int Id { get; set; }
    public string DesignationName { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public StaffDepartment? Department { get; set; }
    public bool IsActive { get; set; } = true;
}
