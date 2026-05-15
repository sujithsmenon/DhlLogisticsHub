namespace DhlLogistics.Shared.Models;

public class LoginResponse
{
    public string   Token     { get; set; } = string.Empty;
    public string   UserId    { get; set; } = string.Empty;
    public string   FullName  { get; set; } = string.Empty;
    public string   Role      { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
