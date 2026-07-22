namespace DhlLogistics.Web.Service
{
    using DhlLogistics.Shared.Models;
    using DhlLogistics.Web.Database;
    using MailKit.Net.Imap;
    using MailKit.Search;
    using Microsoft.EntityFrameworkCore;
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

                // AI Email Automation — Phase 1: store the raw email exactly as
                // received. Additive and self-contained; never blocks the legacy
                // AWB auto-create path below.
                try { await StoreIncomingEmailAsync(message); }
                catch (Exception ex) { Console.WriteLine($"[EmailReader] raw-store failed: {ex.Message}"); }

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

        /// <summary>
        /// Persists an incoming email verbatim (headers, bodies, raw MIME and
        /// attachment bytes). De-duplicated by MessageId so re-polling the same
        /// unread message does not create duplicate rows.
        /// </summary>
        private async Task StoreIncomingEmailAsync(MimeMessage message)
        {
            var messageId = message.MessageId ?? string.Empty;

            if (!string.IsNullOrEmpty(messageId) &&
                await _db.IncomingEmails.AnyAsync(e => e.MessageId == messageId))
            {
                return; // already stored
            }

            var email = new IncomingEmail
            {
                MessageId    = messageId,
                ThreadId     = message.References?.FirstOrDefault()
                                 ?? message.InReplyTo
                                 ?? messageId,
                Subject      = message.Subject ?? string.Empty,
                From         = message.From?.ToString() ?? string.Empty,
                To           = message.To?.ToString() ?? string.Empty,
                Cc           = message.Cc?.ToString() ?? string.Empty,
                ReceivedDate = message.Date.UtcDateTime,
                HtmlBody     = message.HtmlBody,
                TextBody     = message.TextBody,
                ProcessingStatus = EmailProcessingStatus.Received,
            };

            using (var raw = new MemoryStream())
            {
                await message.WriteToAsync(raw);
                email.RawMime = raw.ToArray();
            }

            foreach (var attachment in message.Attachments)
            {
                if (attachment is not MimePart part) continue;

                using var ms = new MemoryStream();
                await part.Content.DecodeToAsync(ms);
                var bytes = ms.ToArray();

                email.Attachments.Add(new IncomingEmailAttachment
                {
                    FileName    = part.FileName ?? string.Empty,
                    ContentType = part.ContentType?.MimeType ?? string.Empty,
                    SizeBytes   = bytes.LongLength,
                    Content     = bytes,
                });
            }

            email.HasAttachments = email.Attachments.Count > 0;

            _db.IncomingEmails.Add(email);
            await _db.SaveChangesAsync();
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
