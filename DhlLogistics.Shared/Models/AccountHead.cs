namespace DhlLogistics.Shared.Models;

public enum AccountGroup
{
    Asset      = 1,
    Liability  = 2,
    Equity     = 3,
    Income     = 4,
    Expense    = 5,
}

public enum DrCr { Debit = 1, Credit = 2 }

/// <summary>
/// Chart of accounts. Each ledger (Cash, Bank, Sales, Customer A/R, etc.)
/// is one row. Receipt/Payment vouchers post against a Cash/Bank row;
/// Journal/Contra can touch any combination.
/// </summary>
public class AccountHead
{
    public int Id { get; set; }

    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;

    public AccountGroup Group { get; set; } = AccountGroup.Asset;

    /// <summary>True if this head represents a bank account (used in Receipt/Payment).</summary>
    public bool IsBank { get; set; }

    /// <summary>True if this head represents cash on hand (used in Receipt/Payment).</summary>
    public bool IsCash { get; set; }

    public decimal OpeningBalance { get; set; }
    public DrCr    OpeningBalanceType { get; set; } = DrCr.Debit;

    public string? Remarks { get; set; }
    public bool IsActive { get; set; } = true;
}
