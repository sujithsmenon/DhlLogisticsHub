namespace DhlLogistics.Web.Database;

using DhlLogistics.Shared.Models;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Idempotent seed of a standard logistics Chart of Accounts. Runs at startup: if a head with a given
/// <see cref="AccountHead.AccountCode"/> is missing it is inserted, so an empty database self-configures
/// and existing installs gain any newly-added head without duplicating what's already there. The
/// <see cref="Codes"/> constants are the single source of truth shared with <c>AccountingService</c>,
/// which resolves heads by code when it auto-posts vouchers — so seed and posting never drift.
/// </summary>
public static class AccountSeed
{
    /// <summary>Stable account codes referenced by the automatic posting engine.</summary>
    public static class Codes
    {
        // Assets
        public const string Cash               = "1000";
        public const string Bank               = "1010";
        public const string AccountsReceivable = "1100";

        // Liabilities
        public const string AccountsPayable    = "2000";
        public const string GstPayable         = "2100";

        // Income
        public const string ForwardingRevenue     = "4000";
        public const string ClearanceRevenue      = "4010";
        public const string TransportationRevenue = "4020";
        public const string OtherServiceRevenue   = "4090";

        // Expenses
        public const string TransportationExpense = "5000";
        public const string CustomsDuty           = "5010";
        public const string PortCharges           = "5020";
        public const string LabourCharges         = "5030";
        public const string DocumentationCharges  = "5040";
        public const string MiscellaneousExpense  = "5090";
    }

    private static readonly (string Code, string Name, AccountGroup Group, bool IsCash, bool IsBank)[] Standard =
    {
        // Assets
        (Codes.Cash,               "Cash",                  AccountGroup.Asset,     true,  false),
        (Codes.Bank,               "Bank",                  AccountGroup.Asset,     false, true ),
        (Codes.AccountsReceivable, "Accounts Receivable",   AccountGroup.Asset,     false, false),
        // Liabilities
        (Codes.AccountsPayable,    "Accounts Payable",      AccountGroup.Liability, false, false),
        (Codes.GstPayable,         "GST Payable",           AccountGroup.Liability, false, false),
        // Income
        (Codes.ForwardingRevenue,     "Forwarding Revenue",     AccountGroup.Income, false, false),
        (Codes.ClearanceRevenue,      "Clearance Revenue",      AccountGroup.Income, false, false),
        (Codes.TransportationRevenue, "Transportation Revenue", AccountGroup.Income, false, false),
        (Codes.OtherServiceRevenue,   "Other Service Revenue",  AccountGroup.Income, false, false),
        // Expenses
        (Codes.TransportationExpense, "Transportation Expense", AccountGroup.Expense, false, false),
        (Codes.CustomsDuty,           "Customs Duty",           AccountGroup.Expense, false, false),
        (Codes.PortCharges,           "Port Charges",           AccountGroup.Expense, false, false),
        (Codes.LabourCharges,         "Labour Charges",         AccountGroup.Expense, false, false),
        (Codes.DocumentationCharges,  "Documentation Charges",  AccountGroup.Expense, false, false),
        (Codes.MiscellaneousExpense,  "Miscellaneous Expense",  AccountGroup.Expense, false, false),
    };

    public static async Task SeedAsync(AppDbContext db)
    {
        var existing = await db.AccountHeads.Select(a => a.AccountCode).ToListAsync();
        var have = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var toAdd = Standard
            .Where(s => !have.Contains(s.Code))
            .Select(s => new AccountHead
            {
                AccountCode        = s.Code,
                AccountName        = s.Name,
                Group              = s.Group,
                IsCash             = s.IsCash,
                IsBank             = s.IsBank,
                OpeningBalance     = 0m,
                OpeningBalanceType = s.Group is AccountGroup.Liability or AccountGroup.Income
                    ? DrCr.Credit    // liabilities & income carry credit balances
                    : DrCr.Debit,    // assets & expenses carry debit balances
                IsActive           = true,
            })
            .ToList();

        if (toAdd.Count == 0) return;

        db.AccountHeads.AddRange(toAdd);
        await db.SaveChangesAsync();
    }
}
