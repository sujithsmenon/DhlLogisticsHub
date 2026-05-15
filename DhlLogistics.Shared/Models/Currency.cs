namespace DhlLogistics.Shared.Models;

public class Currency
{
    public int Id { get; set; }
    public string CurrencyName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty; // ISO 4217: INR, USD, EUR
    public string Symbol { get; set; } = string.Empty;
    public decimal ExchangeRateToInr { get; set; } = 1m;
    public bool IsActive { get; set; } = true;
}
