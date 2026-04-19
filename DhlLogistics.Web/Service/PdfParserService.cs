namespace DhlLogistics.Web.Service
{
    using iText.Kernel.Pdf;
    using iText.Kernel.Pdf.Canvas.Parser;
    using System.Text;
    using System.Text.RegularExpressions;

    public class PdfParserService
    {
        public Task<PickupInfo?> ExtractPickupInfoAsync(byte[] pdfBytes, string fileName)
        {
            using var reader = new PdfReader(new MemoryStream(pdfBytes));
            using var doc = new PdfDocument(reader);

            var sb = new StringBuilder();
            for (int i = 1; i <= doc.GetNumberOfPages(); i++)
                sb.AppendLine(PdfTextExtractor.GetTextFromPage(doc.GetPage(i)));

            var text = sb.ToString();

            // Parse key fields — adjust these patterns to match DHL's actual PDF format
            // Once client provides a sample PDF, update these regex patterns
            var info = new PickupInfo
            {
                RawText = text,
                ClientName = ExtractField(text, @"(?i)client[:\s]+([^\n]+)"),
                PickupAddress = ExtractField(text, @"(?i)pickup address[:\s]+([^\n]+)"),
                PickupCity = ExtractField(text, @"(?i)city[:\s]+([^\n]+)"),
                PickupDate = ExtractDate(text, @"(?i)pickup date[:\s]+([^\n]+)"),
                VolumeCbm = ExtractDouble(text, @"(?i)volume[:\s]+([\d.]+)"),
                WeightKg = ExtractDouble(text, @"(?i)weight[:\s]+([\d.]+)"),
                Destination = ExtractField(text, @"(?i)destination[:\s]+([^\n]+)"),
                DhlReference = ExtractField(text, @"(?i)reference[:\s]+([^\n]+)"),
                SourceFile = fileName
            };

            return Task.FromResult<PickupInfo?>(
                string.IsNullOrWhiteSpace(info.PickupAddress) ? null : info);
        }

        private string ExtractField(string text, string pattern)
        {
            var m = Regex.Match(text, pattern);
            return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
        }

        private DateTime? ExtractDate(string text, string pattern)
        {
            var val = ExtractField(text, pattern);
            return DateTime.TryParse(val, out var dt) ? dt : null;
        }

        private double ExtractDouble(string text, string pattern)
        {
            var val = ExtractField(text, pattern);
            return double.TryParse(val, out var d) ? d : 0;
        }
    }
}
