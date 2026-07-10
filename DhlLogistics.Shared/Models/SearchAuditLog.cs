namespace DhlLogistics.Shared.Models;

/// <summary>Audit trail for the universal search bar: who searched what, when, how long it took, how many
/// results came back, and (when set) which result was opened. Written fire-and-forget so it never slows the
/// search itself. Indexed on <see cref="At"/> for retention/reporting.</summary>
public class SearchAuditLog
{
    public long Id { get; set; }

    public string?   UserName    { get; set; }
    public string    Term        { get; set; } = string.Empty;
    public string?   ModuleHint  { get; set; }   // detected smart-keyword module, if any
    public int       ResultCount { get; set; }
    public int       ElapsedMs   { get; set; }
    public DateTime  At          { get; set; } = DateTime.UtcNow;

    // Populated by a follow-up call when the user opens a result from the dropdown.
    public string?   OpenedModule  { get; set; }
    public string?   OpenedPrimary { get; set; }
}
