namespace DhlLogistics.Web.Database;

using DhlLogistics.Shared.Models;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Seeds realistic Indian freight-forwarding master data. Idempotent — only
/// inserts rows when the relevant table is empty.
/// </summary>
public static class M2SeedData
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await SeedRegionsAsync(db);
        await SeedStatesAsync(db);
        await SeedCountriesAsync(db);
        await SeedCurrenciesAsync(db);
        await SeedPortsAsync(db);
        await SeedContainerSizesAsync(db);
        await SeedSacAsync(db);
        await SeedChargeCodesAsync(db);
        await SeedVehicleDocumentTypesAsync(db);
        await SeedDriverDocumentTypesAsync(db);
        await SeedStaffDepartmentsAsync(db);
        await SeedStaffDesignationsAsync(db);
        await SeedCommoditiesAsync(db);
    }

    static async Task SeedRegionsAsync(AppDbContext db)
    {
        if (await db.Regions.AnyAsync()) return;
        db.Regions.AddRange(
            new Region { RegionName = "North India" },
            new Region { RegionName = "South India" },
            new Region { RegionName = "East India" },
            new Region { RegionName = "West India" },
            new Region { RegionName = "Central India" },
            new Region { RegionName = "North-East India" }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedCountriesAsync(AppDbContext db)
    {
        if (await db.Countries.AnyAsync()) return;
        db.Countries.AddRange(
            new Country { CountryCode = "IN", CountryName = "India" },
            new Country { CountryCode = "US", CountryName = "United States" },
            new Country { CountryCode = "GB", CountryName = "United Kingdom" },
            new Country { CountryCode = "AE", CountryName = "United Arab Emirates" },
            new Country { CountryCode = "SG", CountryName = "Singapore" },
            new Country { CountryCode = "CN", CountryName = "China" },
            new Country { CountryCode = "DE", CountryName = "Germany" },
            new Country { CountryCode = "JP", CountryName = "Japan" },
            new Country { CountryCode = "SA", CountryName = "Saudi Arabia" },
            new Country { CountryCode = "AU", CountryName = "Australia" },
            new Country { CountryCode = "MY", CountryName = "Malaysia" },
            new Country { CountryCode = "TH", CountryName = "Thailand" },
            new Country { CountryCode = "HK", CountryName = "Hong Kong" },
            new Country { CountryCode = "NL", CountryName = "Netherlands" },
            new Country { CountryCode = "FR", CountryName = "France" }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedStatesAsync(AppDbContext db)
    {
        if (await db.States.AnyAsync()) return;
        var regions = await db.Regions.ToDictionaryAsync(r => r.RegionName, r => r.Id);
        int? R(string n) => regions.TryGetValue(n, out var id) ? id : null;

        var south = R("South India"); var north = R("North India");
        var east  = R("East India");  var west  = R("West India");
        var central = R("Central India"); var ne = R("North-East India");

        db.States.AddRange(
            // South
            new State { StateCode = "IN-KL", StateName = "Kerala",         RegionId = south },
            new State { StateCode = "IN-TN", StateName = "Tamil Nadu",     RegionId = south },
            new State { StateCode = "IN-KA", StateName = "Karnataka",      RegionId = south },
            new State { StateCode = "IN-AP", StateName = "Andhra Pradesh", RegionId = south },
            new State { StateCode = "IN-TG", StateName = "Telangana",      RegionId = south },
            new State { StateCode = "IN-PY", StateName = "Puducherry",     RegionId = south },
            // North
            new State { StateCode = "IN-DL", StateName = "Delhi",          RegionId = north },
            new State { StateCode = "IN-HR", StateName = "Haryana",        RegionId = north },
            new State { StateCode = "IN-PB", StateName = "Punjab",         RegionId = north },
            new State { StateCode = "IN-UP", StateName = "Uttar Pradesh",  RegionId = north },
            new State { StateCode = "IN-UK", StateName = "Uttarakhand",    RegionId = north },
            new State { StateCode = "IN-HP", StateName = "Himachal Pradesh", RegionId = north },
            new State { StateCode = "IN-JK", StateName = "Jammu & Kashmir", RegionId = north },
            new State { StateCode = "IN-CH", StateName = "Chandigarh",     RegionId = north },
            // West
            new State { StateCode = "IN-MH", StateName = "Maharashtra",    RegionId = west },
            new State { StateCode = "IN-GJ", StateName = "Gujarat",        RegionId = west },
            new State { StateCode = "IN-RJ", StateName = "Rajasthan",      RegionId = west },
            new State { StateCode = "IN-GA", StateName = "Goa",            RegionId = west },
            // East
            new State { StateCode = "IN-WB", StateName = "West Bengal",    RegionId = east },
            new State { StateCode = "IN-OD", StateName = "Odisha",         RegionId = east },
            new State { StateCode = "IN-BR", StateName = "Bihar",          RegionId = east },
            new State { StateCode = "IN-JH", StateName = "Jharkhand",      RegionId = east },
            // Central
            new State { StateCode = "IN-MP", StateName = "Madhya Pradesh", RegionId = central },
            new State { StateCode = "IN-CG", StateName = "Chhattisgarh",   RegionId = central },
            // NE
            new State { StateCode = "IN-AS", StateName = "Assam",          RegionId = ne },
            new State { StateCode = "IN-MN", StateName = "Manipur",        RegionId = ne },
            new State { StateCode = "IN-ML", StateName = "Meghalaya",      RegionId = ne },
            new State { StateCode = "IN-MZ", StateName = "Mizoram",        RegionId = ne },
            new State { StateCode = "IN-NL", StateName = "Nagaland",       RegionId = ne },
            new State { StateCode = "IN-TR", StateName = "Tripura",        RegionId = ne },
            new State { StateCode = "IN-AR", StateName = "Arunachal Pradesh", RegionId = ne },
            new State { StateCode = "IN-SK", StateName = "Sikkim",         RegionId = ne }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedCurrenciesAsync(AppDbContext db)
    {
        if (await db.Currencies.AnyAsync()) return;
        db.Currencies.AddRange(
            new Currency { CurrencyCode = "INR", CurrencyName = "Indian Rupee",      Symbol = "₹",  ExchangeRateToInr = 1m },
            new Currency { CurrencyCode = "USD", CurrencyName = "US Dollar",         Symbol = "$",  ExchangeRateToInr = 83.5m },
            new Currency { CurrencyCode = "EUR", CurrencyName = "Euro",              Symbol = "€",  ExchangeRateToInr = 90m },
            new Currency { CurrencyCode = "GBP", CurrencyName = "British Pound",     Symbol = "£",  ExchangeRateToInr = 105m },
            new Currency { CurrencyCode = "AED", CurrencyName = "UAE Dirham",        Symbol = "د.إ", ExchangeRateToInr = 22.7m },
            new Currency { CurrencyCode = "SGD", CurrencyName = "Singapore Dollar",  Symbol = "S$", ExchangeRateToInr = 61m },
            new Currency { CurrencyCode = "CNY", CurrencyName = "Chinese Yuan",      Symbol = "¥",  ExchangeRateToInr = 11.5m },
            new Currency { CurrencyCode = "SAR", CurrencyName = "Saudi Riyal",       Symbol = "﷼",  ExchangeRateToInr = 22.2m },
            new Currency { CurrencyCode = "JPY", CurrencyName = "Japanese Yen",      Symbol = "¥",  ExchangeRateToInr = 0.56m },
            new Currency { CurrencyCode = "AUD", CurrencyName = "Australian Dollar", Symbol = "A$", ExchangeRateToInr = 55m }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedPortsAsync(AppDbContext db)
    {
        if (await db.Ports.AnyAsync()) return;
        var inId = await db.Countries.Where(c => c.CountryCode == "IN").Select(c => c.Id).FirstOrDefaultAsync();
        db.Ports.AddRange(
            new Port { PortCode = "INCOK", PortName = "Cochin Port",          City = "Kochi",     Type = PortType.Sea, CountryId = inId },
            new Port { PortCode = "INMAA", PortName = "Chennai Port",         City = "Chennai",   Type = PortType.Sea, CountryId = inId },
            new Port { PortCode = "INNSA", PortName = "Nhava Sheva (JNPT)",   City = "Mumbai",    Type = PortType.Sea, CountryId = inId },
            new Port { PortCode = "INBOM", PortName = "Mumbai Port",          City = "Mumbai",    Type = PortType.Sea, CountryId = inId },
            new Port { PortCode = "INTUT", PortName = "Tuticorin Port",       City = "Tuticorin", Type = PortType.Sea, CountryId = inId },
            new Port { PortCode = "INMUN", PortName = "Mundra Port",          City = "Mundra",    Type = PortType.Sea, CountryId = inId },
            new Port { PortCode = "INVTZ", PortName = "Visakhapatnam Port",   City = "Vizag",     Type = PortType.Sea, CountryId = inId },
            new Port { PortCode = "INKAT", PortName = "Kattupalli Port",      City = "Chennai",   Type = PortType.Sea, CountryId = inId },
            new Port { PortCode = "INPNY", PortName = "Pipavav Port",         City = "Pipavav",   Type = PortType.Sea, CountryId = inId },
            // Airports
            new Port { PortCode = "INCOA", PortName = "Cochin International Airport", City = "Kochi",  Type = PortType.Air, CountryId = inId },
            new Port { PortCode = "INBOA", PortName = "Mumbai CSI Airport",   City = "Mumbai",    Type = PortType.Air, CountryId = inId },
            new Port { PortCode = "INDEL", PortName = "Delhi IGI Airport",    City = "Delhi",     Type = PortType.Air, CountryId = inId },
            new Port { PortCode = "INMAA-A", PortName = "Chennai Airport",    City = "Chennai",   Type = PortType.Air, CountryId = inId },
            new Port { PortCode = "INBLR", PortName = "Bengaluru Airport",    City = "Bengaluru", Type = PortType.Air, CountryId = inId },
            // ICDs
            new Port { PortCode = "INICTT", PortName = "ICTT Vallarpadam",    City = "Kochi",     Type = PortType.ICD, CountryId = inId },
            new Port { PortCode = "INTKD", PortName = "ICD Tughlakabad",      City = "Delhi",     Type = PortType.ICD, CountryId = inId }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedContainerSizesAsync(AppDbContext db)
    {
        if (await db.ContainerSizes.AnyAsync()) return;
        db.ContainerSizes.AddRange(
            new ContainerSize { SizeName = "20ft Standard",    ShortCode = "20GP", TeuFactor = 1m,  PayloadKg = 28200m },
            new ContainerSize { SizeName = "40ft Standard",    ShortCode = "40GP", TeuFactor = 2m,  PayloadKg = 28600m },
            new ContainerSize { SizeName = "40ft High Cube",   ShortCode = "40HC", TeuFactor = 2m,  PayloadKg = 28560m },
            new ContainerSize { SizeName = "45ft High Cube",   ShortCode = "45HC", TeuFactor = 2.25m, PayloadKg = 27600m },
            new ContainerSize { SizeName = "20ft Reefer",      ShortCode = "20RF", TeuFactor = 1m,  PayloadKg = 27600m },
            new ContainerSize { SizeName = "40ft Reefer",      ShortCode = "40RF", TeuFactor = 2m,  PayloadKg = 27700m },
            new ContainerSize { SizeName = "20ft Open Top",    ShortCode = "20OT", TeuFactor = 1m,  PayloadKg = 28100m },
            new ContainerSize { SizeName = "40ft Flat Rack",   ShortCode = "40FR", TeuFactor = 2m,  PayloadKg = 39800m },
            new ContainerSize { SizeName = "20ft Tank",        ShortCode = "20TK", TeuFactor = 1m,  PayloadKg = 26000m }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedSacAsync(AppDbContext db)
    {
        if (await db.Sacs.AnyAsync()) return;
        db.Sacs.AddRange(
            new Sac { SacCode = "996712", Description = "Sea / coastal freight transport of containers",      GstRate = 5m },
            new Sac { SacCode = "996713", Description = "Sea / coastal freight transport of bulk cargo",      GstRate = 5m },
            new Sac { SacCode = "996721", Description = "Air freight transport of letters and parcels",       GstRate = 18m },
            new Sac { SacCode = "996722", Description = "Air freight transport of other goods",               GstRate = 18m },
            new Sac { SacCode = "996791", Description = "Cargo handling services",                            GstRate = 18m },
            new Sac { SacCode = "996793", Description = "Container handling services",                        GstRate = 18m },
            new Sac { SacCode = "998540", Description = "Customs House Agent services",                       GstRate = 18m },
            new Sac { SacCode = "996601", Description = "Rental of road vehicle with operator",               GstRate = 18m },
            new Sac { SacCode = "996711", Description = "Sea transport of passengers",                        GstRate = 18m },
            new Sac { SacCode = "996731", Description = "Rail freight transport",                             GstRate = 5m }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedChargeCodesAsync(AppDbContext db)
    {
        if (await db.ChargeCodes.AnyAsync()) return;
        var sacs = await db.Sacs.ToDictionaryAsync(s => s.SacCode, s => s.Id);
        int? S(string code) => sacs.TryGetValue(code, out var id) ? id : null;

        db.ChargeCodes.AddRange(
            new ChargeCode { ShortCode = "OFR",   ChargeName = "Ocean Freight",            SacId = S("996712"), DefaultAmount = 0m },
            new ChargeCode { ShortCode = "AFR",   ChargeName = "Air Freight",              SacId = S("996722"), DefaultAmount = 0m },
            new ChargeCode { ShortCode = "THC",   ChargeName = "Terminal Handling Charge", SacId = S("996793"), DefaultAmount = 0m },
            new ChargeCode { ShortCode = "BL",    ChargeName = "Bill of Lading Fee",       SacId = S("996793"), DefaultAmount = 1500m },
            new ChargeCode { ShortCode = "DOC",   ChargeName = "Documentation",            SacId = S("998540"), DefaultAmount = 750m },
            new ChargeCode { ShortCode = "CHA",   ChargeName = "Customs Clearance",        SacId = S("998540"), DefaultAmount = 3500m },
            new ChargeCode { ShortCode = "TRPT",  ChargeName = "Transportation",           SacId = S("996601"), DefaultAmount = 0m },
            new ChargeCode { ShortCode = "DEM",   ChargeName = "Demurrage",                SacId = S("996793"), DefaultAmount = 0m },
            new ChargeCode { ShortCode = "DET",   ChargeName = "Detention",                SacId = S("996793"), DefaultAmount = 0m },
            new ChargeCode { ShortCode = "INSP",  ChargeName = "Inspection / Survey",      SacId = S("996791"), DefaultAmount = 1200m },
            new ChargeCode { ShortCode = "FUMG",  ChargeName = "Fumigation",               SacId = S("996791"), DefaultAmount = 2500m },
            new ChargeCode { ShortCode = "STMP",  ChargeName = "Stamp Charges",            SacId = S("998540"), DefaultAmount = 250m }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedVehicleDocumentTypesAsync(AppDbContext db)
    {
        if (await db.VehicleDocumentTypes.AnyAsync()) return;
        db.VehicleDocumentTypes.AddRange(
            new VehicleDocumentType { DocumentTypeName = "Registration Certificate (RC)" },
            new VehicleDocumentType { DocumentTypeName = "Insurance" },
            new VehicleDocumentType { DocumentTypeName = "Permit (National/State)" },
            new VehicleDocumentType { DocumentTypeName = "Fitness Certificate" },
            new VehicleDocumentType { DocumentTypeName = "Pollution (PUC)" },
            new VehicleDocumentType { DocumentTypeName = "Road Tax" }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedDriverDocumentTypesAsync(AppDbContext db)
    {
        if (await db.DriverDocumentTypes.AnyAsync()) return;
        db.DriverDocumentTypes.AddRange(
            new DriverDocumentType { DocumentTypeName = "Driving Licence (DL)" },
            new DriverDocumentType { DocumentTypeName = "Aadhaar Card",      HasExpiry = false },
            new DriverDocumentType { DocumentTypeName = "PAN Card",          HasExpiry = false },
            new DriverDocumentType { DocumentTypeName = "Police Verification" },
            new DriverDocumentType { DocumentTypeName = "Medical Fitness" }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedStaffDepartmentsAsync(AppDbContext db)
    {
        if (await db.StaffDepartments.AnyAsync()) return;
        db.StaffDepartments.AddRange(
            new StaffDepartment { DepartmentName = "Operations" },
            new StaffDepartment { DepartmentName = "Sales & Marketing" },
            new StaffDepartment { DepartmentName = "Accounts & Finance" },
            new StaffDepartment { DepartmentName = "Customs Clearance" },
            new StaffDepartment { DepartmentName = "Documentation" },
            new StaffDepartment { DepartmentName = "Fleet" },
            new StaffDepartment { DepartmentName = "Human Resources" },
            new StaffDepartment { DepartmentName = "IT" }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedStaffDesignationsAsync(AppDbContext db)
    {
        if (await db.StaffDesignations.AnyAsync()) return;
        var deps = await db.StaffDepartments.ToDictionaryAsync(d => d.DepartmentName, d => d.Id);
        int? D(string n) => deps.TryGetValue(n, out var id) ? id : null;

        db.StaffDesignations.AddRange(
            new StaffDesignation { DesignationName = "Operations Manager",     DepartmentId = D("Operations") },
            new StaffDesignation { DesignationName = "Operations Executive",   DepartmentId = D("Operations") },
            new StaffDesignation { DesignationName = "Operations Coordinator", DepartmentId = D("Operations") },
            new StaffDesignation { DesignationName = "Sales Manager",          DepartmentId = D("Sales & Marketing") },
            new StaffDesignation { DesignationName = "Sales Executive",        DepartmentId = D("Sales & Marketing") },
            new StaffDesignation { DesignationName = "Accountant",             DepartmentId = D("Accounts & Finance") },
            new StaffDesignation { DesignationName = "Senior Accountant",      DepartmentId = D("Accounts & Finance") },
            new StaffDesignation { DesignationName = "Customs Officer",        DepartmentId = D("Customs Clearance") },
            new StaffDesignation { DesignationName = "Documentation Executive", DepartmentId = D("Documentation") },
            new StaffDesignation { DesignationName = "Fleet Manager",          DepartmentId = D("Fleet") },
            new StaffDesignation { DesignationName = "Driver",                 DepartmentId = D("Fleet") },
            new StaffDesignation { DesignationName = "HR Manager",             DepartmentId = D("Human Resources") }
        );
        await db.SaveChangesAsync();
    }

    static async Task SeedCommoditiesAsync(AppDbContext db)
    {
        if (await db.Commodities.AnyAsync()) return;
        db.Commodities.AddRange(
            new Commodity { CommodityName = "Textiles & Apparel",          HsCode = "61"   },
            new Commodity { CommodityName = "Pharmaceutical Products",     HsCode = "30"   },
            new Commodity { CommodityName = "Electronics & Components",    HsCode = "85"   },
            new Commodity { CommodityName = "Machinery & Mechanical",      HsCode = "84"   },
            new Commodity { CommodityName = "Spices & Tea",                HsCode = "0904" },
            new Commodity { CommodityName = "Seafood & Marine Products",   HsCode = "03"   },
            new Commodity { CommodityName = "Rubber & Articles",           HsCode = "40"   },
            new Commodity { CommodityName = "Coir & Coir Products",        HsCode = "5305" },
            new Commodity { CommodityName = "Chemicals (Non-Hazardous)",   HsCode = "29"   },
            new Commodity { CommodityName = "Chemicals (Hazardous)",       HsCode = "29",  IsHazardous = true },
            new Commodity { CommodityName = "Automotive Parts",            HsCode = "87"   },
            new Commodity { CommodityName = "Plastic Products",            HsCode = "39"   },
            new Commodity { CommodityName = "Frozen Vegetables",           HsCode = "0710" },
            new Commodity { CommodityName = "Cashew Kernels",              HsCode = "0801" }
        );
        await db.SaveChangesAsync();
    }
}
