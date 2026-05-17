namespace DhlLogistics.Web.Api;

using DhlLogistics.Web.Service;

/// <summary>
/// Read-only AWB shipment + Export-job endpoints mirroring those two grids
/// on the web dashboard. Mutations stay on the web app.
/// </summary>
public static class ShipmentEndpoints
{
    public static void MapShipmentEndpoints(this WebApplication app)
    {
        // ── AWB Shipments ────────────────────────────────────────────────────
        var awb = app.MapGroup("/api/awb").RequireAuthorization("MobileAdminApi");
        awb.MapGet("/",          async (AwbShipmentService s) => Results.Ok(await s.GetAllAsync()));
        awb.MapGet("/{id:int}",  async (int id, AwbShipmentService s) =>
            await s.GetAsync(id) is { } a ? Results.Ok(a) : Results.NotFound());
        awb.MapGet("/transporters",
                                 async (AwbShipmentService s) => Results.Ok(await s.GetAllTransportersAsync()));

        // ── Export Jobs ──────────────────────────────────────────────────────
        var exp = app.MapGroup("/api/exports").RequireAuthorization("MobileAdminApi");
        exp.MapGet("/",          async (ExportJobService s) => Results.Ok(await s.GetAllAsync()));
        exp.MapGet("/{id:int}",  async (int id, ExportJobService s) =>
            await s.GetAsync(id) is { } j ? Results.Ok(j) : Results.NotFound());
        exp.MapGet("/transporters",
                                 async (ExportJobService s) => Results.Ok(await s.GetTransportersAsync()));
    }
}
