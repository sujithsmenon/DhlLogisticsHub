namespace DhlLogistics.Web.Service
{
    using DhlLogistics.Web.Database;
    using MailKit.Net.Imap;
    using MailKit.Search;
    using MimeKit;

    public class EmailReaderService
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;
        private readonly PdfParserService _pdfParser;
        private readonly JobAssignmentService _jobs;

        public EmailReaderService(IConfiguration config, AppDbContext db,
            PdfParserService pdfParser, JobAssignmentService jobs)
        {
            _config = config; _db = db;
            _pdfParser = pdfParser; _jobs = jobs;
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

            // Fetch only unread emails
            var uids = await inbox.SearchAsync(SearchQuery.NotSeen);

            foreach (var uid in uids)
            {
                var message = await inbox.GetMessageAsync(uid);

                // Log the email
                var log = new EmailLog
                {
                    Subject = message.Subject,
                    From = message.From.ToString(),
                    ReceivedAt = message.Date.UtcDateTime,
                    HasAttachment = message.Attachments.Any()
                };

                // Process PDF attachments
                foreach (var attachment in message.Attachments)
                {
                    if (attachment is MimePart part &&
                        part.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        using var ms = new MemoryStream();
                        await part.Content.DecodeToAsync(ms);
                        var pdfBytes = ms.ToArray();

                        // Parse pickup details from PDF
                        var pickupInfo = await _pdfParser.ExtractPickupInfoAsync(pdfBytes, part.FileName);

                        if (pickupInfo != null)
                        {
                            // Auto-create a job from the parsed data
                            await _jobs.CreateJobFromEmailAsync(pickupInfo, message.Subject);
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

    // Background service — polls every 5 minutes
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
                await svc.CheckInboxAsync();
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
            }
        }
    }
}
