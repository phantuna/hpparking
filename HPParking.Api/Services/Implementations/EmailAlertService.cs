using HPParking.Api.Common.Email;
using HPParking.Api.Configuration;
using HPParking.Api.DTOs.Alerts;
using HPParking.Api.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System.IO;
using System.Threading.Channels;

namespace HPParking.Api.Services.Implementations
{
    public class EmailAlertService : BackgroundService, IEmailAlertService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailAlertService> _logger;
        private readonly Channel<Func<CancellationToken, Task>> _mailQueue;

        public EmailAlertService(
            IOptions<EmailSettings> emailOptions,
            ILogger<EmailAlertService> logger)
        {
            _emailSettings = emailOptions.Value;
            _logger = logger;
            // Kênh hàng đợi không giới hạn để tiếp nhận request gửi email ngầm
            _mailQueue = Channel.CreateUnbounded<Func<CancellationToken, Task>>(new UnboundedChannelOptions
            {
                SingleReader = true
            });
        }

        public void EnqueueInterGateAlert(InterGateTripAlertModel model)
        {
            _mailQueue.Writer.TryWrite(ct => SendInterGateTripAlertAsync(model, ct));
            _logger.LogInformation("[EmailAlert] Đã đưa vào hàng đợi cảnh báo liên cổng cho xe {Plate}", model.PlateNumber);
        }

        public void EnqueueLateCheckInAlert(LateCheckInAlertModel model)
        {
            _mailQueue.Writer.TryWrite(ct => SendLateCheckInAlertAsync(model, ct));
            _logger.LogInformation("[EmailAlert] Đã đưa vào hàng đợi cảnh báo vào cổng muộn cho nhân viên {Name}", model.EmployeeName);
        }

        public async Task SendInterGateTripAlertAsync(InterGateTripAlertModel model, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ValidateConfig()) return;

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                foreach (var recipient in _emailSettings.AlertRecipients)
                {
                    if (!string.IsNullOrWhiteSpace(recipient))
                        message.To.Add(MailboxAddress.Parse(recipient.Trim()));
                }

                message.Subject = $"[HPParking Cảnh Báo] Xe {model.PlateNumber} di chuyển liên cổng quá giờ (+{model.OverdueMinutes:F1}p)";

                var builder = new BodyBuilder();
                string cid = "overview_evidence_" + Guid.NewGuid().ToString("N");

                if (!string.IsNullOrWhiteSpace(model.OverviewImagePath) && File.Exists(model.OverviewImagePath))
                {
                    var image = builder.LinkedResources.Add(model.OverviewImagePath);
                    image.ContentId = cid;
                }
                else
                {
                    // Fallback transparent 1x1 image nếu không tìm thấy file ảnh
                    builder.LinkedResources.Add("no_image.png", new byte[0]).ContentId = cid;
                }

                builder.HtmlBody = EmailTemplateBuilder.BuildInterGateTripAlertHtml(model, cid);
                message.Body = builder.ToMessageBody();

                await SendEmailMessageAsync(message, cancellationToken);
                _logger.LogInformation("[EmailAlert] Đã gửi thành công email cảnh báo liên cổng cho xe {Plate}", model.PlateNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EmailAlert] Lỗi khi gửi email cảnh báo liên cổng cho xe {Plate}", model.PlateNumber);
            }
        }

        public async Task SendLateCheckInAlertAsync(LateCheckInAlertModel model, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ValidateConfig()) return;

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                foreach (var recipient in _emailSettings.AlertRecipients)
                {
                    if (!string.IsNullOrWhiteSpace(recipient))
                        message.To.Add(MailboxAddress.Parse(recipient.Trim()));
                }

                message.Subject = $"[HPParking Cảnh Báo] Nhân viên {model.EmployeeName} vào cổng muộn (+{model.LateMinutes}p)";

                var builder = new BodyBuilder();
                string cid = "overview_late_" + Guid.NewGuid().ToString("N");

                if (!string.IsNullOrWhiteSpace(model.OverviewImagePath) && File.Exists(model.OverviewImagePath))
                {
                    var image = builder.LinkedResources.Add(model.OverviewImagePath);
                    image.ContentId = cid;
                }
                else
                {
                    builder.LinkedResources.Add("no_image.png", new byte[0]).ContentId = cid;
                }

                builder.HtmlBody = EmailTemplateBuilder.BuildLateCheckInAlertHtml(model, cid);
                message.Body = builder.ToMessageBody();

                await SendEmailMessageAsync(message, cancellationToken);
                _logger.LogInformation("[EmailAlert] Đã gửi thành công email cảnh báo vào cổng muộn cho nhân viên {Name}", model.EmployeeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EmailAlert] Lỗi khi gửi email cảnh báo vào cổng muộn cho nhân viên {Name}", model.EmployeeName);
            }
        }

        private async Task SendEmailMessageAsync(MimeMessage message, CancellationToken cancellationToken)
        {
            using var client = new SmtpClient();
            
            // Kết nối Gmail SMTP (STARTTLS trên cổng 587)
            var secureOption = _emailSettings.SmtpPort == 465 
                ? SecureSocketOptions.SslOnConnect 
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, secureOption, cancellationToken);
            
            if (!string.IsNullOrWhiteSpace(_emailSettings.AppPassword))
            {
                await client.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.AppPassword, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }

        private bool ValidateConfig()
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.SenderEmail) || _emailSettings.AlertRecipients.Count == 0)
            {
                _logger.LogWarning("[EmailAlert] Chưa cấu hình SenderEmail hoặc danh sách AlertRecipients trong appsettings.json. Bỏ qua gửi email.");
                return false;
            }
            return true;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[EmailAlert] Background Worker xử lý hàng đợi email đã khởi động.");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var task = await _mailQueue.Reader.ReadAsync(stoppingToken);
                    await task(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[EmailAlert] Lỗi không mong muốn trong vòng lặp Background Worker.");
                }
            }
        }
    }
}
