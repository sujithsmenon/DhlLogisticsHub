namespace DhlLogistics.Shared.Models;

public class Staff
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public StaffDepartment? Department { get; set; }
    public int? DesignationId { get; set; }
    public StaffDesignation? Designation { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public bool IsActive { get; set; } = true;
}
