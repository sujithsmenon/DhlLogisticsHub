namespace DhlLogistics.Shared.Models;

public class AwbShipment
{
    public int Id { get; set; }

    // ── AWB Identity ──────────────────────────────────────────────────────────
    public string HawbNo { get; set; } = "";
    public DateTime? IssuedDate { get; set; }
    public string StationCode { get; set; } = "";

    // ── Shipper ───────────────────────────────────────────────────────────────
    public string ShipperAccount { get; set; } = "";
    public string ShipperName { get; set; } = "";
    public string ShipperAddress { get; set; } = "";
    public string ShipperPhone { get; set; } = "";
    public string ShipperContact { get; set; } = "";

    // ── Consignee ─────────────────────────────────────────────────────────────
    public string ConsigneeAccount { get; set; } = "";
    public string ConsigneeName { get; set; } = "";
    public string ConsigneeAddress { get; set; } = "";
    public string ConsigneePhone { get; set; } = "";
    public string ConsigneeContact { get; set; } = "";

    // ── Route ─────────────────────────────────────────────────────────────────
    public string OriginStation { get; set; } = "";
    public string DestinationStation { get; set; } = "";

    // ── Reference ─────────────────────────────────────────────────────────────
    public string ReferenceNumbers { get; set; } = "";
    public string HandlingInfo { get; set; } = "";

    // ── Cargo ─────────────────────────────────────────────────────────────────
    public int Pieces { get; set; }
    public double GrossWeightKg { get; set; }
    public double ChargeableWeight { get; set; }
    public string RateClass { get; set; } = "";
    public string GoodsDescription { get; set; } = "";
    public string HsCode { get; set; } = "";
    public string Dimensions { get; set; } = "";
    public double VolumeCbm { get; set; }
    public int Slac { get; set; }

    // ── Financial ─────────────────────────────────────────────────────────────
    public string Currency { get; set; } = "USD";
    public string DeclaredValueCarriage { get; set; } = "";
    public string DeclaredValueCustoms { get; set; } = "";

    // ── System ────────────────────────────────────────────────────────────────
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string SourceEmail { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string RawText { get; set; } = "";

    // ── Workflow Status ───────────────────────────────────────────────────────
    public AwbStatus Status { get; set; } = AwbStatus.Received;

    // ── Stage 1: Transporter Assignment ──────────────────────────────────────
    public int? TransporterId { get; set; }
    public Transporter? Transporter { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropLocation { get; set; }
    public DateTime? TransporterAssignedAt { get; set; }

    // ── Stage 2: Vehicle Details (received from transporter via WhatsApp) ─────
    public string? VehicleNumber { get; set; }
    public string? DriverName { get; set; }
    public string? DriverMobile { get; set; }
    public DateTime? VehicleDetailsAt { get; set; }

    // ── Stage 3: Delivery at Cochin Customs Port ──────────────────────────────
    public DateTime? DeliveredAtPortAt { get; set; }
    public string? DeliveryPhotoPath { get; set; }
    public string? GodownReceiptPath { get; set; }

    // ── Stage 4: Customs Papers ───────────────────────────────────────────────
    public DateTime? CustomsDocsReceivedAt { get; set; }
    public string? CustomsDocPath { get; set; }

    // ── Stage 5: Invoice to DHL ───────────────────────────────────────────────
    public DateTime? InvoiceSentAt { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? InvoiceFilePath { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public List<ShipmentEvent> Events { get; set; } = [];
}

public enum AwbStatus
{
    Received,
    TransporterAssigned,
    VehicleAssigned,
    InTransit,
    DeliveredAtPort,
    CustomsPending,
    CustomsCleared,
    InvoiceSent,
    Completed
}
