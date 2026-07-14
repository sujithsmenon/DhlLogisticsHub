namespace DhlLogistics.Web.Components.Common;

/// <summary>
/// The single status → colour-slug map for the whole ERP.
///
/// Consumed by the full-cell grid colouring (SfGrid QueryCellInfo → td class
/// "st-cell-{slug}") and by <see cref="StatusBadge"/>. Screens must never carry
/// their own status colour switch — add new statuses HERE and give the slug a
/// token pair in app.css.
///
/// Takes object so any module's enum works (JobOperationStatus, BillStatus,
/// VoucherStatus, …) without this file depending on any of them.
/// </summary>
public static class StatusKind
{
    /// <summary>Unknown / unmapped statuses fall through to "unknown" (neutral grey).</summary>
    public static string Of(object? status)
    {
        var key = status?.ToString();
        if (string.IsNullOrWhiteSpace(key)) return "unknown";

        // Normalise so "In Progress", in_progress and InProgress all collide.
        key = new string(key.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        return key switch
        {
            "draft"                                  => "draft",
            "pending" or "open" or "new"             => "pending",
            "assigned"                               => "assigned",
            "inprogress" or "processing" or "active" => "inprogress",
            "verified"                               => "verified",
            "approved" or "posted"                   => "approved",
            "completed" or "complete" or "done"      => "completed",
            "issued"                                 => "issued",
            // A superseded invoice is history, not a live document — it reads as retired, not as an error.
            "superseded" or "notissued"              => "superseded",
            "onhold" or "hold" or "suspended"        => "onhold",
            "delayed" or "overdue" or "late"         => "delayed",
            "cancelled" or "canceled" or "void"      => "cancelled",
            "rejected" or "declined"                 => "rejected",
            "closed" or "settled"                    => "closed",
            _                                        => "unknown",
        };
    }
}
