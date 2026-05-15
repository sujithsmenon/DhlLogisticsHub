namespace DhlLogistics.Shared.Models;

public class ExportJob
{
    public int Id { get; set; }

    // ── Job Identity ──────────────────────────────────────────────────────────
    public string JobReference { get; set; } = "";      // DHL Pick Confirmation No.
    public string CustomerName { get; set; } = "";
    public string CargoDescription { get; set; } = "";
    public string HsCode { get; set; } = "";
    public int Pieces { get; set; }
    public double GrossWeightKg { get; set; }
    public bool IsEmergency { get; set; }
    public string Notes { get; set; } = "";

    // ── System ────────────────────────────────────────────────────────────────
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public ExportJobStatus Status { get; set; } = ExportJobStatus.Received;

    // ── Stage 2: Pickup Initiated ─────────────────────────────────────────────
    public DateTime? PickupInitiatedAt { get; set; }

    // ── Stage 3: Transporter Booked ───────────────────────────────────────────
    public int? TransporterId { get; set; }
    public Transporter? Transporter { get; set; }
    public DateTime? TransporterBookedAt { get; set; }

    // ── Stage 4: Vehicle Details ──────────────────────────────────────────────
    public string? VehicleNumber { get; set; }
    public string? DriverName { get; set; }
    public string? DriverMobile { get; set; }
    public DateTime? VehicleAssignedAt { get; set; }

    // ── Stage 5: Cargo Collected + RI ────────────────────────────────────────
    public string? RiNumber { get; set; }               // Release Intimation number
    public DateTime? CargoCollectedAt { get; set; }

    // ── Stage 6: Checklist (Focus Software) + Customer Confirmation ───────────
    public string? ChecklistRef { get; set; }           // Focus Software checklist ref
    public DateTime? ChecklistSentAt { get; set; }
    public DateTime? CustomerConfirmedAt { get; set; }

    // ── Stage 7: ICEGATE + Shipping Bill ─────────────────────────────────────
    public string? IcegateRef { get; set; }
    public string? ShippingBillNumber { get; set; }
    public DateTime? ShippingBillAt { get; set; }

    // ── Stage 8: Handover to Operation Staff ─────────────────────────────────
    public string? OperationStaffName { get; set; }
    public DateTime? HandedOverAt { get; set; }

    // ── Stage 9: Port Clearance ───────────────────────────────────────────────
    public DateTime? PortArrivedAt { get; set; }
    public DateTime? PortClearedAt { get; set; }

    // ── Stage 10: Export Ready ────────────────────────────────────────────────
    public DateTime? ExportReadyAt { get; set; }

    // ── Stage 11: Booking Receipt ─────────────────────────────────────────────
    public string? BookingReference { get; set; }
    public DateTime? BookingReceivedAt { get; set; }

    // ── Stage 12: Container Survey ────────────────────────────────────────────
    public string? SurveyTeamName { get; set; }
    public DateTime? SurveyForwardedAt { get; set; }
    public DateTime? SurveyCompletedAt { get; set; }

    // ── Stage 13: Loading Authorization ──────────────────────────────────────
    public string? LoadingAuthReference { get; set; }      // Customs/Port auth ref
    public DateTime? LoadingAuthAt { get; set; }

    // ── Stage 14: Cargo Loading ───────────────────────────────────────────────
    public string? ContainerNumber { get; set; }
    public string? SealNumber { get; set; }
    public DateTime? CargoLoadedAt { get; set; }

    // ── Stage 15: SI and DO Distribution ─────────────────────────────────────
    public string? ShippingInstruction { get; set; }       // SI ref from DHL
    public string? DeliveryOrderRef { get; set; }          // DO ref from DHL
    public string? ThirdPartyName { get; set; }            // SEZ-4 handler
    public DateTime? SiDoReceivedAt { get; set; }

    // ── Stage 16: Transport to ICTT ───────────────────────────────────────────
    public DateTime? InTransitToIcttAt { get; set; }

    // ── Stage 17: Terminal Gate-In ────────────────────────────────────────────
    public string? Sez4Reference { get; set; }
    public string? VesselName { get; set; }
    public string? VoyageNumber { get; set; }
    public DateTime? TerminalGateInAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public List<ExportJobEvent> Events { get; set; } = [];
}

public enum ExportJobStatus
{
    Received,           // Step 1:  DHL Pick Confirmation received
    PickupInitiated,    // Step 2:  Pickup formally initiated (Emergency status)
    TransporterBooked,  // Step 3:  Transporter dispatched
    VehicleAssigned,    // Step 4:  Vehicle details recorded
    CargoCollected,     // Step 5:  Cargo collected + RI submitted to Cochin Port
    ChecklistPending,   // Step 6a: Checklist generated & sent, awaiting customer confirmation
    ChecklistConfirmed, // Step 6b: Customer confirmed checklist
    IcegateSubmitted,   // Step 7:  Submitted to ICEGATE, Shipping Bill applied
    HandedToOps,        // Step 8:  Checklist + SB No. handed to Operation Staff
    PortCleared,        // Step 9:  Customs & terminal clearance at Cochin Port complete
    ExportReady,        // Step 10: Export clearance received, ready for vessel loading
    BookingReceived,    // Step 11: Official booking confirmation received
    SurveyInProgress,   // Step 12: Booking forwarded to Survey Team for container inspection
    LoadingAuthorized,  // Step 13: Permission from Customs + Cochin Port Authority to stuff
    CargoLoaded,        // Step 14: Cargo physically loaded into container, loading complete
    SiDoReceived,       // Step 15: SI + DO received from DHL, shared with third party for SEZ-4
    InTransitToIctt,    // Step 16: Loaded container in transit to ICTT Vallarpadam
    TerminalGateIn      // Step 17: SEZ-4 issued, container gated in at ICTT, awaiting vessel
}

public class ExportJobEvent
{
    public int Id { get; set; }
    public int ExportJobId { get; set; }
    public ExportJob? Job { get; set; }
    public string EventType { get; set; } = "";
    public string Description { get; set; } = "";
    public string? FilePath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByName { get; set; }
}
