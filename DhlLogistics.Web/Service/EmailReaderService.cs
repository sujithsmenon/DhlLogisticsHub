namespace DhlLogistics.Web.Service
{
    using DhlLogistics.Shared.Models;
    using DhlLogistics.Web.Database;
    using MailKit.Net.Imap;
    using MailKit.Search;
    using MimeKit;

    public class EmailReaderService
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;
        private readonly PdfParserService _pdfParser;
        private readonly NotificationService _notify;

        public EmailReaderService(IConfiguration config, AppDbContext db,
            PdfParserService pdfParser, NotificationService notify)
        {
            _config    = config;
            _db        = db;
            _pdfParser = pdfParser;
            _notify    = notify;
        }

        public async Task CheckInboxAsync()
        {
            var settings = _config.GetSection("EmailSettings");

            using var client = new ImapClient();
            await client.ConnectAsync(settings["ImapHost"],
                int.Parse(settings["ImapPort"]!), true);
            await client.AuthenticateAsync(settings["Username"], settings["Password"]);

            var inbox = client.Inbox;
            await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);

            var uids = await inbox.SearchAsync(SearchQuery.NotSeen);

            foreach (var uid in uids)
            {
                var message = await inbox.GetMessageAsync(uid);

                var log = new EmailLog
                {
                    Subject      = message.Subject,
                    From         = message.From.ToString(),
                    ReceivedAt   = message.Date.UtcDateTime,
                    HasAttachment = message.Attachments.Any()
                };

                foreach (var attachment in message.Attachments)
                {
                    if (attachment is MimePart part &&
                        part.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        using var ms = new MemoryStream();
                        await part.Content.DecodeToAsync(ms);
                        var pdfBytes = ms.ToArray();

                        var awb = await _pdfParser.ExtractAwbAsync(pdfBytes, part.FileName);

                        if (awb != null)
                        {
                            awb.SourceEmail = message.From.ToString();
                            awb.ReceivedAt  = message.Date.UtcDateTime;

                            _db.AwbShipments.Add(awb);
                            await _db.SaveChangesAsync();

                            await _notify.NotifyManagersAsync(
                                title:   "New AWB Received",
                                body:    $"{awb.HawbNo} — {awb.GoodsDescription}, {awb.OriginStation} → {awb.DestinationStation}",
                                type:    "NewAwb",
                                jobId:   awb.Id,
                                jobCode: awb.HawbNo);

                            log.JobCreated = true;
                        }
                    }
                }

                _db.EmailLogs.Add(log);
            }

            await _db.SaveChangesAsync();
            await client.DisconnectAsync(true);
        }
    }

    public class EmailPollingService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        public EmailPollingService(IServiceProvider sp) => _sp = sp;

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                using var scope = _sp.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<EmailReaderService>();
                try { await svc.CheckInboxAsync(); } catch { /* log in production */ }
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
            }
        }
    }
}
