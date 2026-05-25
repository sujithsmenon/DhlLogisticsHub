namespace DhlLogistics.Shared.Models;

public class VoucherLine
{
    public long Id { get; set; }

    public long VoucherId { get; set; }
    public Voucher? Voucher { get; set; }

    public int AccountHeadId { get; set; }
    public AccountHead? AccountHead { get; set; }

    public DrCr    DrCr   { get; set; } = DrCr.Debit;
    public decimal Amount { get; set; }

    public string? Narration { get; set; }
    public int DisplayOrder  { get; set; }
}
