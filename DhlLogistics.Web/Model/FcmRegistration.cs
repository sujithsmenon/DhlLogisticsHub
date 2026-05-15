namespace DhlLogistics.Web.Model;

public class FcmRegistration
{
    public int    Id       { get; set; }
    public string UserId   { get; set; } = string.Empty;
    public string Token    { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;  // "android" | "ios"
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
