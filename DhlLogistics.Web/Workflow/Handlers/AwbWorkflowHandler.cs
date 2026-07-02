namespace DhlLogistics.Web.Workflow.Handlers;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;

/// <summary>
/// Workflow adapter for AWB shipments. Covers manual Create and the details Edit (patch of the
/// editable header fields — status and workflow timestamps are left to the dedicated workflow
/// actions on <see cref="Service.AwbShipmentService"/>). No numbering, no billing.
/// </summary>
public sealed class AwbWorkflowHandler : IWorkflowHandler
{
    private readonly AppDbContext _db;
    public AwbWorkflowHandler(AppDbContext db) => _db = db;

    public string Module     => "AWB Shipment";
    public string EntityType => "AwbShipment";

    private static AwbShipment Awb(IWorkflowContext ctx) => (AwbShipment)ctx.Entity;

    public Task ValidateAsync(IWorkflowContext ctx)
    {
        var awb = Awb(ctx);
        if (ctx.Operation == WorkflowOperationType.Create && string.IsNullOrWhiteSpace(awb.HawbNo))
            ctx.Abort("HAWB number is required.");
        return Task.CompletedTask;
    }

    public Task GenerateNumberAsync(IWorkflowContext ctx) => Task.CompletedTask;   // HAWB is user-entered

    public async Task PersistAsync(IWorkflowContext ctx)
    {
        var awb = Awb(ctx);
        switch (ctx.Operation)
        {
            case WorkflowOperationType.Create:
                awb.Id         = 0;
                awb.ReceivedAt = DateTime.UtcNow;
                awb.Status     = AwbStatus.Received;
                _db.AwbShipments.Add(awb);
                await _db.SaveChangesAsync();
                break;

            case WorkflowOperationType.Update:
                var existing = await _db.AwbShipments.FindAsync(awb.Id);
                if (existing is null) { ctx.Abort($"AWB #{awb.Id} not found."); return; }
                CopyEditableFields(awb, existing);
                await _db.SaveChangesAsync();
                break;
        }
    }

    public Task GenerateBillingAsync(IWorkflowContext ctx) => Task.CompletedTask;   // no billing for AWB

    public async Task WriteTimelineAsync(IWorkflowContext ctx)
    {
        var awb = Awb(ctx);
        _db.Set<ShipmentEvent>().Add(new ShipmentEvent
        {
            AwbShipmentId = awb.Id,
            EventType     = ctx.Operation == WorkflowOperationType.Create ? "Created" : "Updated",
            Description   = ctx.Operation == WorkflowOperationType.Create
                                ? $"AWB {awb.HawbNo} received."
                                : $"AWB {awb.HawbNo} details updated.",
            CreatedByName = ctx.User,
            CreatedAt     = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    public WorkflowDescriptor Describe(IWorkflowContext ctx)
    {
        var awb  = Awb(ctx);
        var verb = ctx.Operation.ToString().ToLowerInvariant() + "d";
        return new WorkflowDescriptor(awb.Id, awb.HawbNo, $"{Module} {awb.HawbNo} {verb}.");
    }

    // Same editable set as the former AwbShipmentService.UpdateAwbDetailsAsync patch — deliberately
    // excludes Status / transporter / workflow timestamps (those are owned by the workflow actions).
    private static void CopyEditableFields(AwbShipment src, AwbShipment dst)
    {
        dst.HawbNo               = src.HawbNo;
        dst.IssuedDate           = src.IssuedDate;
        dst.StationCode          = src.StationCode;
        dst.ShipperAccount       = src.ShipperAccount;
        dst.ShipperName          = src.ShipperName;
        dst.ShipperAddress       = src.ShipperAddress;
        dst.ShipperPhone         = src.ShipperPhone;
        dst.ShipperContact       = src.ShipperContact;
        dst.ConsigneeAccount     = src.ConsigneeAccount;
        dst.ConsigneeName        = src.ConsigneeName;
        dst.ConsigneeAddress     = src.ConsigneeAddress;
        dst.ConsigneePhone       = src.ConsigneePhone;
        dst.ConsigneeContact     = src.ConsigneeContact;
        dst.OriginStation        = src.OriginStation;
        dst.DestinationStation   = src.DestinationStation;
        dst.ReferenceNumbers     = src.ReferenceNumbers;
        dst.HandlingInfo         = src.HandlingInfo;
        dst.Pieces               = src.Pieces;
        dst.GrossWeightKg        = src.GrossWeightKg;
        dst.ChargeableWeight     = src.ChargeableWeight;
        dst.RateClass            = src.RateClass;
        dst.GoodsDescription     = src.GoodsDescription;
        dst.HsCode               = src.HsCode;
        dst.Dimensions           = src.Dimensions;
        dst.VolumeCbm            = src.VolumeCbm;
        dst.Slac                 = src.Slac;
        dst.Currency             = src.Currency;
        dst.DeclaredValueCarriage = src.DeclaredValueCarriage;
        dst.DeclaredValueCustoms  = src.DeclaredValueCustoms;
    }
}
