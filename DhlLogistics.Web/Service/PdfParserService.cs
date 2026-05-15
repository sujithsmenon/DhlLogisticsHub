namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using System.Text;
using System.Text.RegularExpressions;

public class PdfParserService
{
    // Compiled patterns for DHL House Air Waybill format
    private static readonly Regex RxHawb        = new(@"HAWB\s*No\.\s*:\s*([A-Z0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxShipperAcct = new(@"(\w+)\s+House Air Waybill", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxStationCode = new(@"Station Code:\s*(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxStations    = new(@"(?:Origin Station[^\n]*\n)([A-Z]+)\s+([A-Z]+)", RegexOptions.Compiled);
    private static readonly Regex RxReference   = new(@"Reference Number\(s\)[^\n]*\n([^\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxCargo       = new(@"(\d+)\s+([\d.]+)K\s+([A-Z])\s+([\d.]+)\s+As Agreed\s+As Agreed\s+([^\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxHsCode      = new(@"HS Codes?:\s*([\d.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxDims        = new(@"DIMS\s+([^\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxVolume      = new(@"VOL\s+([\d.]+)\s*M3", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxSlac        = new(@"(\d+)\s+SLAC", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxCurrency    = new(@"\n(USD|EUR|GBP|INR)\s+(\w+)\s+(\w+)", RegexOptions.Compiled);
    private static readonly Regex RxDate        = new(@"(\d{1,2}-\w{3}-\d{2,4})\s+\w+\s+DHL", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxTe          = new(@"TE\s+(\+[\d]+)\s+([A-Za-z][^\n]+)", RegexOptions.Compiled);
    private static readonly Regex RxConsigneeAcct = new(@"Consignee's Account Number[^\n]*\n([^\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<AwbShipment?> ExtractAwbAsync(byte[] pdfBytes, string fileName)
    {
        using var reader = new PdfReader(new MemoryStream(pdfBytes));
        using var doc    = new PdfDocument(reader);

        var sb = new StringBuilder();
        for (int i = 1; i <= doc.GetNumberOfPages(); i++)
            sb.AppendLine(PdfTextExtractor.GetTextFromPage(doc.GetPage(i)));

        var text = sb.ToString();

        var awb = new AwbShipment
        {
            SourceFile = fileName,
            RawText    = text,
        };

        // ── HAWB No. ──────────────────────────────────────────────────────────
        awb.HawbNo = Field(RxHawb, text);

        // ── Shipper Account ───────────────────────────────────────────────────
        awb.ShipperAccount = Field(RxShipperAcct, text);

        // ── Station Code ──────────────────────────────────────────────────────
        awb.StationCode = Field(RxStationCode, text);

        // ── Origin / Destination ──────────────────────────────────────────────
        var stationsM = RxStations.Match(text);
        if (stationsM.Success)
        {
            awb.OriginStation      = stationsM.Groups[1].Value.Trim();
            awb.DestinationStation = stationsM.Groups[2].Value.Trim();
        }

        // ── Reference Numbers ─────────────────────────────────────────────────
        awb.ReferenceNumbers = Field(RxReference, text).Trim();

        // ── Consignee Account ─────────────────────────────────────────────────
        awb.ConsigneeAccount = Field(RxConsigneeAcct, text).Trim();

        // ── Phone / Contact — first TE = shipper, second TE = consignee ───────
        var tematches = RxTe.Matches(text);
        if (tematches.Count > 0)
        {
            awb.ShipperPhone   = tematches[0].Groups[1].Value.Trim();
            awb.ShipperContact = tematches[0].Groups[2].Value.Trim();
        }
        if (tematches.Count > 1)
        {
            awb.ConsigneePhone   = tematches[1].Groups[1].Value.Trim();
            awb.ConsigneeContact = tematches[1].Groups[2].Value.Trim();
        }

        // ── Currency / Declared Values ────────────────────────────────────────
        var currM = RxCurrency.Match(text);
        if (currM.Success)
        {
            awb.Currency              = currM.Groups[1].Value;
            awb.DeclaredValueCarriage = currM.Groups[2].Value;
            awb.DeclaredValueCustoms  = currM.Groups[3].Value;
        }

        // ── Cargo line: pieces / weight / rate / chargeable / description ─────
        var cargoM = RxCargo.Match(text);
        if (cargoM.Success)
        {
            awb.Pieces            = int.TryParse(cargoM.Groups[1].Value, out var p) ? p : 0;
            awb.GrossWeightKg     = double.TryParse(cargoM.Groups[2].Value, out var gw) ? gw : 0;
            awb.RateClass         = cargoM.Groups[3].Value;
            awb.ChargeableWeight  = double.TryParse(cargoM.Groups[4].Value, out var cw) ? cw : 0;
            awb.GoodsDescription  = cargoM.Groups[5].Value.Trim();
        }

        // ── HS Code ───────────────────────────────────────────────────────────
        awb.HsCode = Field(RxHsCode, text);

        // ── Dimensions ────────────────────────────────────────────────────────
        awb.Dimensions = Field(RxDims, text).Trim();

        // ── Volume ────────────────────────────────────────────────────────────
        awb.VolumeCbm = Dbl(RxVolume, text);

        // ── SLAC ──────────────────────────────────────────────────────────────
        awb.Slac = Int(RxSlac, text);

        // ── Issued Date ───────────────────────────────────────────────────────
        var dateStr = Field(RxDate, text);
        if (DateTime.TryParse(dateStr, out var dt))
            awb.IssuedDate = dt;

        // ── Extract Shipper Name & Address from raw text block ─────────────────
        ParseShipperConsigneeBlocks(text, awb);

        return Task.FromResult<AwbShipment?>(
            string.IsNullOrWhiteSpace(awb.HawbNo) ? null : awb);
    }

    // Extracts shipper name/address and consignee name/address from known text positions
    private static void ParseShipperConsigneeBlocks(string text, AwbShipment awb)
    {
        // Shipper name appears between "House Air Waybill\n" and "Issued by"
        var shipperNameM = Regex.Match(text,
            @"House Air Waybill\s*\r?\n([^\r\n]+?)\s+Issued by",
            RegexOptions.IgnoreCase);
        if (shipperNameM.Success)
            awb.ShipperName = shipperNameM.Groups[1].Value.Trim();

        // Consignee block: text between consignee account and "Notify"
        // After the consignee account line, grab next 3 lines as name + address
        if (!string.IsNullOrWhiteSpace(awb.ConsigneeAccount))
        {
            var acctIdx = text.IndexOf(awb.ConsigneeAccount, StringComparison.OrdinalIgnoreCase);
            if (acctIdx >= 0)
            {
                var afterAcct = text[(acctIdx + awb.ConsigneeAccount.Length)..];
                var lines = afterAcct.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                    awb.ConsigneeName = lines[0].Trim();
                var addrParts = lines.Skip(1)
                    .TakeWhile(l => !l.StartsWith("TE ", StringComparison.OrdinalIgnoreCase)
                                 && !l.StartsWith("Notify", StringComparison.OrdinalIgnoreCase))
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0)
                    .ToList();
                awb.ConsigneeAddress = string.Join(", ", addrParts);
            }
        }

        // Shipper address: lines between station code line and "TE" (shipper TE)
        // Heuristic: grab the block that starts with a house number after the DHL address block
        var shipperAddrM = Regex.Match(text,
            @"(?:US\s+Station Code:\s*\w+\s*\r?\n)(\d+[^\r\n]+\r?\n[^\r\n]+)",
            RegexOptions.IgnoreCase);
        if (shipperAddrM.Success)
            awb.ShipperAddress = shipperAddrM.Groups[1].Value.Replace("\r", "").Replace("\n", ", ").Trim();
    }

    private static string Field(Regex rx, string text)
    {
        var m = rx.Match(text);
        return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
    }

    private static double Dbl(Regex rx, string text)
    {
        var s = Field(rx, text);
        return double.TryParse(s, out var d) ? d : 0;
    }

    private static int Int(Regex rx, string text)
    {
        var s = Field(rx, text);
        return int.TryParse(s, out var i) ? i : 0;
    }
}
