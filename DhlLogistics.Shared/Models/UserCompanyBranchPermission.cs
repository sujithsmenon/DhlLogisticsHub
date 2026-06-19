namespace DhlLogistics.Shared.Models;

/// <summary>
/// Grants a registered user access to a <see cref="CompanyBranch"/>. Ported from CBM.
/// <see cref="UserId"/> references <see cref="RegisterdUser.UserId"/>.
/// </summary>
public class UserCompanyBranchPermission
{
    public int Id { get; set; }
    public int UserId { get; set; }    // -> RegisterdUser.UserId
    public int BranchId { get; set; }  // -> CompanyBranch.Id
}
