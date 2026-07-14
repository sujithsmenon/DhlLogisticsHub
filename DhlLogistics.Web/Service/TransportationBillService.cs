namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Maps any shipment/job type (AWB shipment, Export job, Clearance/Forwarding JobOrder) into a
/// <b>prepared</b> Transportation <see cref="Bill"/> — a Draft in memory only. Persistence, numbering,
/// verification, approval and accounting all reuse the existing Bill workflow (<see cref="BillService"/>,
/// <see cref="Workflow.Handlers.BillWorkflowHandler"/>, <see cref="AccountingService"/>): this service adds
/// no billing logic, it only fills the bill's header + transport snapshot from source data. That keeps a
/// single billing/approval/accounts pipeline while letting every shipment type raise a Transportation bill
/// without a separate module or duplicated code.
/// </summary>
public class TransportationBillService
{
    private readonly AppDbContext _db;

    public TransportationBillService(AppDbContext db) => _db = db;

    /// <summary>Active transporters for the bill's transporter picker (reused from the AWB/Export masters).</summary>
    public Task<List<Transporter>> GetTransportersAsync() =>
        _db.Transporters.Where(t => t.IsActive).OrderBy(t => t.CompanyName).ToListAsync();

    // ── Source → prepared Transportation Bill ─────────────────────────────────

    /// <summary>Prepare a Draft Transportation bill from an AWB shipment. Billing client / currency are left
    /// for the user to pick (AWB stores parties as free text, not client FKs); everything else is snapshotted.</summary>
    public async Task<Bill> PrepareFromAwbAsync(long awbId)
    {
        var a = await _db.AwbShipments
            .Include(x => x.Transporter)
            .FirstOrDefaultAsync(x => x.Id == (int)awbId)
            ?? throw new InvalidOperationException("AWB shipment not found.");

        var bill = NewTransportBill();
        bill.SourceType       = BillSourceType.AwbShipment;
        bill.SourceId         = a.Id;
        bill.SourceReference  = a.HawbNo;
        bill.ShipmentTypeName = "AWB Shipment (Air)";
        bill.AwbOrBlNumber    = a.HawbNo;
        bill.Reference        = a.ConsigneeName;              // visible until a billing client is chosen
        bill.Origin           = a.OriginStation;
        bill.Destination      = a.DestinationStation;
        bill.PickupLocation   = a.PickupLocation;
        bill.DeliveryLocation = a.DropLocation;
        bill.VehicleNumber    = a.VehicleNumber;
        bill.DriverName       = a.DriverName;
        bill.TransporterId    = a.TransporterId;
        bill.CommodityName    = a.GoodsDescription;
        bill.Quantity         = a.Pieces;
        bill.WeightKg         = (decimal)a.GrossWeightKg;
        bill.VolumeCbm        = (decimal)a.VolumeCbm;
        // The CUSTOMER's reference (Billing Group key) — NOT a.InvoiceNumber, which is the Stage-5 invoice we
        // raise TO DHL. Seeding the group key from that would file the bill under our own invoice number.
        bill.CustomerInvoiceNumber = a.CustomerInvoiceNumber;
        bill.Remarks          = a.HandlingInfo;
        return bill;
    }

    /// <summary>Prepare a Draft Transportation bill from an Export job.</summary>
    public async Task<Bill> PrepareFromExportAsync(long exportId)
    {
        var e = await _db.ExportJobs
            .Include(x => x.Transporter)
            .FirstOrDefaultAsync(x => x.Id == (int)exportId)
            ?? throw new InvalidOperationException("Export job not found.");

        var bill = NewTransportBill();
        bill.SourceType       = BillSourceType.ExportJob;
        bill.SourceId         = e.Id;
        bill.SourceReference  = e.JobReference;
        bill.ShipmentTypeName = "Export Job";
        bill.AwbOrBlNumber    = e.ShippingBillNumber;
        bill.ContainerNumber  = e.ContainerNumber;
        bill.Reference        = e.CustomerName;
        bill.Destination      = e.VesselName;                 // export routing: vessel/voyage as destination
        bill.VehicleNumber    = e.VehicleNumber;
        bill.DriverName       = e.DriverName;
        bill.TransporterId    = e.TransporterId;
        bill.CommodityName    = e.CargoDescription;
        bill.Quantity         = e.Pieces;
        bill.WeightKg         = (decimal)e.GrossWeightKg;
        bill.CustomerInvoiceNumber = e.CustomerInvoiceNumber;   // Billing Group key
        bill.Remarks          = e.Notes;
        return bill;
    }

    /// <summary>Base Draft Transportation bill with a single seeded Freight charge line, so the bill satisfies
    /// the workflow's "at least one charge" rule and the user only enters the amount before submitting.</summary>
    private static Bill NewTransportBill()
    {
        var bill = new Bill
        {
            Mode         = BillMode.Transportation,
            BillDate     = DateTime.UtcNow.Date,
            ExchangeRate = 1m,
        };
        bill.Charges.Add(new BillCharge
        {
            Category     = ChargeCategory.Transport,
            Description  = "Freight Charges",
            Quantity     = 1m,
            Rate         = 0m,
            GstRate      = 0m,
            DisplayOrder = 1,
        });
        return bill;
    }
}
