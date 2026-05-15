namespace DhlLogistics.Shared.Models;

public class Transporter
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = "";
    public string ContactPerson { get; set; } = "";
    public string Phone { get; set; } = "";
    public string WhatsAppNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
