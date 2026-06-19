namespace DhlLogistics.Shared.Models;

/// <summary>
/// Grants a registered user access to a <see cref="ShipmentActivity"/>. Ported from
/// CBM. <see cref="UserId"/> references <see cref="RegisterdUser.UserId"/>.
/// </summary>
public class UserShipmentActivityPermission
{
    public int Id { get; set; }
    public int UserId { get; set; }      // -> RegisterdUser.UserId
    public int ActivityId { get; set; }  // -> ShipmentActivity.Id
}
