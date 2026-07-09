namespace DhlLogistics.Shared.Models;

/// <summary>
/// Read model exposing a single <see cref="JobOrder"/>'s Cargo Totals (gross weight / volume /
/// estimated value / currency / remarks). Lets downstream consumers — Invoice/Bill PDF, the billing
/// popup, reports, dashboards, and the future Customer Portal / Mobile App — read these values without
/// depending on the JobOrder entity. Presentation/read-only; no business logic.
/// </summary>
public class JobCargoInfoDto
{
    public long     JobOrderId     { get; set; }
    public string   JobOrderNo     { get; set; } = string.Empty;
    public decimal? GrossWeightKg  { get; set; }
    public decimal? VolumeCbm      { get; set; }
    public decimal? EstimatedValue { get; set; }
    public int?     CurrencyId     { get; set; }
    public string?  CurrencyCode   { get; set; }
    public string?  Remarks        { get; set; }

    public static JobCargoInfoDto FromJob(JobOrder j) => new()
    {
        JobOrderId     = j.Id,
        JobOrderNo     = j.JobOrderNo,
        GrossWeightKg  = j.GrossWeightKg,
        VolumeCbm      = j.VolumeCbm,
        EstimatedValue = j.EstimatedValue,
        CurrencyId     = j.CurrencyId,
        CurrencyCode   = j.Currency?.CurrencyCode,
        Remarks        = j.Remarks,
    };
}

/// <summary>
/// Aggregate cargo totals across a set of jobs — feeds dashboard KPI tiles
/// (Total Cargo Weight / Volume / Value) and is reusable by future clients.
/// </summary>
public class CargoTotalsDto
{
    public int     JobCount            { get; set; }
    public decimal TotalGrossWeightKg  { get; set; }
    public decimal TotalVolumeCbm      { get; set; }
    public decimal TotalEstimatedValue { get; set; }
}
