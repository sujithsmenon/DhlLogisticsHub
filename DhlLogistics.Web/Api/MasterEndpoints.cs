namespace DhlLogistics.Web.Api;

using DhlLogistics.Web.Service;

/// <summary>
/// Read-only endpoints that mirror every master grid on the web dashboard to
/// the mobile app. Gated by MobileAdminApi (JWT + Admin/Manager role).
///
/// Pattern is one GET list + one GET by id per master. Mutations stay on the
/// web app — mobile is view-only for these.
/// </summary>
public static class MasterEndpoints
{
    public static void MapMasterEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/masters").RequireAuthorization("MobileAdminApi");

        // ── Geography ────────────────────────────────────────────────────────
        g.MapGet("/countries",      async (CountryService s)      => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/countries/{id:int}",
                                    async (int id, CountryService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/regions",        async (RegionService s)       => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/regions/{id:int}",
                                    async (int id, RegionService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/states",         async (StateService s)        => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/states/{id:int}",
                                    async (int id, StateService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/ports",          async (PortService s)         => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/ports/{id:int}", async (int id, PortService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/sez-locations",  async (SezLocationService s)  => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/sez-locations/{id:int}",
                                    async (int id, SezLocationService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        // ── Finance / Tax ────────────────────────────────────────────────────
        g.MapGet("/currencies",     async (CurrencyService s)     => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/currencies/{id:int}",
                                    async (int id, CurrencyService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/sacs",           async (SacService s)          => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/sacs/{id:int}",  async (int id, SacService s)  => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/charge-codes",   async (ChargeCodeService s)   => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/charge-codes/{id:int}",
                                    async (int id, ChargeCodeService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        // ── Operations catalogues ────────────────────────────────────────────
        g.MapGet("/container-sizes",async (ContainerSizeService s) => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/container-sizes/{id:int}",
                                    async (int id, ContainerSizeService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/commodities",    async (CommodityService s)    => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/commodities/{id:int}",
                                    async (int id, CommodityService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/vessels",        async (VesselService s)       => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/vessels/{id:int}",
                                    async (int id, VesselService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        // ── Fleet ────────────────────────────────────────────────────────────
        g.MapGet("/vehicle-drivers",async (VehicleDriverService s) => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/vehicle-drivers/{id:int}",
                                    async (int id, VehicleDriverService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/vehicle-document-types",
                                    async (VehicleDocumentTypeService s) => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/vehicle-document-types/{id:int}",
                                    async (int id, VehicleDocumentTypeService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/vehicle-documents",
                                    async (VehicleDocumentService s) => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/vehicle-documents/{id:int}",
                                    async (int id, VehicleDocumentService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/driver-document-types",
                                    async (DriverDocumentTypeService s) => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/driver-document-types/{id:int}",
                                    async (int id, DriverDocumentTypeService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        // ── HR ───────────────────────────────────────────────────────────────
        g.MapGet("/staff-departments",
                                    async (StaffDepartmentService s) => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/staff-departments/{id:int}",
                                    async (int id, StaffDepartmentService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/staff-designations",
                                    async (StaffDesignationService s) => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/staff-designations/{id:int}",
                                    async (int id, StaffDesignationService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/staff",          async (StaffService s)        => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/staff/{id:int}", async (int id, StaffService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        // ── CBM-parity stubs ─────────────────────────────────────────────────
        g.MapGet("/company-branches",
                                    async (CompanyBranchService s) => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/company-branches/{id:int}",
                                    async (int id, CompanyBranchService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapGet("/shipment-activities",
                                    async (ShipmentActivityService s) => Results.Ok(await s.GetAllAsync()));
        g.MapGet("/shipment-activities/{id:int}",
                                    async (int id, ShipmentActivityService s) => await s.GetByIdAsync(id) is { } e ? Results.Ok(e) : Results.NotFound());
    }
}
