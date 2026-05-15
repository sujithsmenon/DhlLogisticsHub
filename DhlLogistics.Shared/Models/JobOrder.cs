namespace DhlLogistics.Shared.Models;

public enum JobMode { Clearance = 1, Forwarding = 2 }

public enum JobShipmentMode { Sea = 1, Air = 2, Road = 3 }

public enum JobShipmentType { Import = 1, Export = 2 }

public enum JobCargoType { FCL = 1, LCL = 2, Air = 3, Bulk = 4, Break = 5 }

public enum JobOrderStatus
{
    Draft       = 0,
    Submitted   = 10,   // sent for verification
    Verified    = 20,   // ops verified, awaits approval
    Approved    = 30,   // approved for billing
    Rejected    = 40,
    Closed      = 50,   // job completed (after billing)
    Reopened    = 60,   // post-verify modification requested
}

public class JobOrder
{
    public long Id { get; set; }

    public JobMode          Mode         { get; set; } = JobMode.Clearance;
    public JobShipmentMode  ShipmentMode { get; set; } = JobShipmentMode.Sea;
    public JobShipmentType  ShipmentType { get; set; } = JobShipmentType.Import;
    public JobCargoType     CargoType    { get; set; } = JobCargoType.FCL;

    /// <summary>Auto-generated per FY, e.g. "CLR/26-27/0001" or "FWD/26-27/0023".</summary>
    public string JobOrderNo { get; set; } = string.Empty;

    public DateTime JobOrderDate { get; set; } = DateTime.UtcNow.Date;

    /// <summary>Indian FY starting year, e.g. 2026 = FY 2026-27.</summary>
    public int FinYear { get; set; }

    // ── Branch / Activity (CBM stubs) ────────────────────────────────────────
    public int? BranchId { get; set; }
    public CompanyBranch? Branch { get; set; }

    // ── Parties (all DhlClient for now; CBM uses Organization+Branch) ────────
    public int  BillingClientId { get; set; }
    public DhlClient? BillingClient { get; set; }

    public int  ShipperId { get; set; }
    public DhlClient? Shipper { get; set; }

    public int  ConsigneeId { get; set; }
    public DhlClient? Consignee { get; set; }

    public int? SaleStaffId { get; set; }
    public Staff? SaleStaff { get; set; }

    // ── Ports / Routing ──────────────────────────────────────────────────────
    public int? LoadPortId { get; set; }
    public Port? LoadPort { get; set; }

    public int? DischargePortId { get; set; }
    public Port? DischargePort { get; set; }

    // ── Cargo ────────────────────────────────────────────────────────────────
    public int? CommodityId { get; set; }
    public Commodity? Commodity { get; set; }

    public string CargoDescription { get; set; } = string.Empty;

    public int? ContainerSizeId { get; set; }
    public ContainerSize? ContainerSize { get; set; }

    public decimal? LclUnits { get; set; }
    public string?  LclUnitType { get; set; }   // "CBM", "Pkgs", etc.

    public decimal? GrossWeightKg { get; set; }
    public decimal? VolumeCbm     { get; set; }

    // ── Currency ─────────────────────────────────────────────────────────────
    public int? CurrencyId { get; set; }
    public Currency? Currency { get; set; }

    public decimal? EstimatedValue { get; set; }

    // ── Status / lifecycle ───────────────────────────────────────────────────
    public JobOrderStatus Status { get; set; } = JobOrderStatus.Draft;
    public bool IsNominated     { get; set; }   // nominated by overseas agent
    public bool IsEmergency     { get; set; }

    // Audit trail
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public string?  CreatedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
    public string?   ModifiedBy { get; set; }

    public DateTime? SubmittedOn { get; set; }
    public string?   SubmittedBy { get; set; }

    public DateTime? VerifiedOn { get; set; }
    public string?   VerifiedBy { get; set; }

    public DateTime? ApprovedOn { get; set; }
    public string?   ApprovedBy { get; set; }

    public DateTime? RejectedOn       { get; set; }
    public string?   RejectedBy       { get; set; }
    public string?   RejectionReason  { get; set; }

    public DateTime? ClosedOn { get; set; }
    public string?   ClosedBy { get; set; }

    public string? Remarks { get; set; }

    public List<JobOrderEvent> Events { get; set; } = new();
}
