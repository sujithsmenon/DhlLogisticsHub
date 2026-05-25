namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

public class VoucherService
{
    private readonly AppDbContext _db;
    private readonly AuthenticationStateProvider _authProvider;

    public VoucherService(AppDbContext db, AuthenticationStateProvider authProvider)
    {
        _db = db;
        _authProvider = authProvider;
    }

    private async Task<string> CurrentUserAsync()
    {
        var s = await _authProvider.GetAuthenticationStateAsync();
        return s.User?.Identity?.Name ?? "system";
    }

    // ── Queries ──────────────────────────────────────────────────────────────
    private IQueryable<Voucher> WithRefs() => _db.Vouchers
        .Include(v => v.Branch)
        .Include(v => v.CashOrBankAccount)
        .Include(v => v.Party);

    public Task<List<Voucher>> GetByTypeAsync(params VoucherType[] types) =>
        WithRefs().Where(v => types.Contains(v.Type)).OrderByDescending(v => v.Id).ToListAsync();

    public Task<List<Voucher>> GetByStatusAsync(params VoucherStatus[] statuses) =>
        WithRefs().Where(v => statuses.Contains(v.Status)).OrderByDescending(v => v.Id).ToListAsync();

    public Task<Voucher?> GetByIdAsync(long id) =>
        WithRefs()
            .Include(v => v.Lines).ThenInclude(l => l.AccountHead)
            .Include(v => v.Events)
            .FirstOrDefaultAsync(v => v.Id == id);

    // ── Numbering ────────────────────────────────────────────────────────────
    public static int ComputeFinYear(DateTime d) => d.Month >= 4 ? d.Year : d.Year - 1;

    private static string Prefix(VoucherType t) => t switch
    {
        VoucherType.Journal => "JV",
        VoucherType.Receipt => "RV",
        VoucherType.Payment => "PV",
        VoucherType.Contra  => "CV",
        _ => "VV",
    };

    private async Task<string> NextVoucherNoAsync(VoucherType type, int finYear)
    {
        var prefix    = Prefix(type);
        var fyDisplay = $"{(finYear % 100):D2}-{((finYear + 1) % 100):D2}";

        var lastNo = await _db.Vouchers
            .Where(v => v.Type == type && v.FinYear == finYear)
            .OrderByDescending(v => v.Id)
            .Select(v => v.VoucherNo)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (!string.IsNullOrEmpty(lastNo))
        {
            var tail = lastNo.Split('/').Last();
            if (int.TryParse(tail, out var n)) seq = n + 1;
        }
        return $"{prefix}/{fyDisplay}/{seq:D4}";
    }

    // ── Totals + validation ──────────────────────────────────────────────────
    public static void RecalcTotals(Voucher v)
    {
        v.TotalDebit  = v.Lines.Where(l => l.DrCr == DrCr.Debit ).Sum(l => l.Amount);
        v.TotalCredit = v.Lines.Where(l => l.DrCr == DrCr.Credit).Sum(l => l.Amount);
    }

    public static void EnsureBalanced(Voucher v)
    {
        RecalcTotals(v);
        if (v.Lines.Count < 2)
            throw new InvalidOperationException("A voucher needs at least 2 lines.");
        if (v.Lines.Any(l => l.AccountHeadId <= 0))
            throw new InvalidOperationException("Every line needs an account head.");
        if (v.Lines.Any(l => l.Amount <= 0))
            throw new InvalidOperationException("Every line needs an amount greater than 0.");
        if (decimal.Round(v.TotalDebit, 2) != decimal.Round(v.TotalCredit, 2))
            throw new InvalidOperationException(
                $"Debit ({v.TotalDebit:N2}) and Credit ({v.TotalCredit:N2}) totals must match.");
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────
    public async Task<Voucher> CreateAsync(Voucher v)
    {
        var user = await CurrentUserAsync();
        v.FinYear   = ComputeFinYear(v.VoucherDate);
        v.VoucherNo = await NextVoucherNoAsync(v.Type, v.FinYear);
        v.Status    = VoucherStatus.Draft;
        v.CreatedBy = user;
        v.CreatedOn = DateTime.UtcNow;
        EnsureBalanced(v);

        int order = 1;
        foreach (var l in v.Lines) if (l.DisplayOrder == 0) l.DisplayOrder = order++;

        _db.Vouchers.Add(v);
        await _db.SaveChangesAsync();
        await LogEventAsync(v.Id, VoucherEventType.Created, "Voucher created as Draft.", user);
        return v;
    }

    public async Task UpdateAsync(Voucher v)
    {
        var user = await CurrentUserAsync();
        v.ModifiedBy = user;
        v.ModifiedOn = DateTime.UtcNow;
        EnsureBalanced(v);

        _db.Entry(v).State = EntityState.Modified;

        var existing = await _db.VoucherLines.Where(l => l.VoucherId == v.Id).ToListAsync();
        _db.VoucherLines.RemoveRange(existing);

        int order = 1;
        foreach (var l in v.Lines)
        {
            l.Id = 0;
            l.VoucherId = v.Id;
            if (l.DisplayOrder == 0) l.DisplayOrder = order;
            order++;
            _db.VoucherLines.Add(l);
        }

        await _db.SaveChangesAsync();
        await LogEventAsync(v.Id, VoucherEventType.Updated, null, user);
    }

    public async Task SubmitAsync(long id)
    {
        var user = await CurrentUserAsync();
        var v = await _db.Vouchers.FindAsync(id) ?? throw new KeyNotFoundException();
        if (v.Status != VoucherStatus.Draft && v.Status != VoucherStatus.Rejected)
            throw new InvalidOperationException($"Cannot submit a voucher in '{v.Status}' status.");

        v.Status      = VoucherStatus.Submitted;
        v.SubmittedBy = user;
        v.SubmittedOn = DateTime.UtcNow;
        v.RejectedBy  = null; v.RejectedOn = null; v.RejectionReason = null;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, VoucherEventType.Submitted, "Submitted for verification.", user);
    }

    public async Task VerifyAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var v = await _db.Vouchers.FindAsync(id) ?? throw new KeyNotFoundException();
        if (v.Status != VoucherStatus.Submitted)
            throw new InvalidOperationException($"Only Submitted vouchers can be verified (current: {v.Status}).");

        v.Status     = VoucherStatus.Verified;
        v.VerifiedBy = user;
        v.VerifiedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, VoucherEventType.Verified, note, user);
    }

    public async Task ApproveAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var v = await _db.Vouchers.FindAsync(id) ?? throw new KeyNotFoundException();
        if (v.Status != VoucherStatus.Verified)
            throw new InvalidOperationException($"Only Verified vouchers can be approved (current: {v.Status}).");

        v.Status     = VoucherStatus.Approved;
        v.ApprovedBy = user;
        v.ApprovedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, VoucherEventType.Approved, note, user);
    }

    public async Task RejectAsync(long id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Rejection reason is required.");

        var user = await CurrentUserAsync();
        var v = await _db.Vouchers.FindAsync(id) ?? throw new KeyNotFoundException();
        if (v.Status != VoucherStatus.Submitted && v.Status != VoucherStatus.Verified)
            throw new InvalidOperationException($"Cannot reject a voucher in '{v.Status}' status.");

        v.Status          = VoucherStatus.Rejected;
        v.RejectedBy      = user;
        v.RejectedOn      = DateTime.UtcNow;
        v.RejectionReason = reason;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, VoucherEventType.Rejected, reason, user);
    }

    public async Task PostAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var v = await _db.Vouchers.FindAsync(id) ?? throw new KeyNotFoundException();
        if (v.Status != VoucherStatus.Approved)
            throw new InvalidOperationException($"Only Approved vouchers can be posted (current: {v.Status}).");

        v.Status   = VoucherStatus.Posted;
        v.PostedBy = user;
        v.PostedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, VoucherEventType.Posted, note, user);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var v = await _db.Vouchers.FindAsync(id);
        if (v is null) return false;
        if (v.Status != VoucherStatus.Draft)
            throw new InvalidOperationException("Only Draft vouchers can be deleted.");

        _db.Vouchers.Remove(v);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Events ───────────────────────────────────────────────────────────────
    private async Task LogEventAsync(long voucherId, VoucherEventType type, string? notes, string actor)
    {
        _db.VoucherEvents.Add(new VoucherEvent
        {
            VoucherId = voucherId,
            EventType = type,
            Notes     = notes,
            Actor     = actor,
            At        = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }
}
