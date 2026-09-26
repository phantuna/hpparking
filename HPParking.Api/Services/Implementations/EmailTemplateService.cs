using HPParking.Api.Configuration;
using HPParking.Api.DTOs.EmailTemplates;
using HPParking.Api.Services.Interfaces;
using HPParking.Api.Common.Exceptions;
using HPParking.Core.Interfaces;
using HPParking.Core.Models.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HPParking.Api.Services.Implementations
{
    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly IRepository<EmailTemplate> _templateRepository;
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailTemplateService> _logger;

        private static readonly string[] StandardVariables = new[]
        {
            "vehicle_code",
            "plate_number",
            "tag_code",
            "tag_epc",
            "origin_gate",
            "origin_gate_code",
            "destination_gate",
            "destination_gate_code",
            "checkout_origin_time",
            "checkin_dest_time",
            "checkout_dest_time",
            "checkin_origin_time",
            "deadline_checkin_dest",
            "deadline_checkout_dest",
            "deadline_checkin_origin",
            "travel_to_dest_minutes",
            "stay_at_dest_minutes",
            "travel_back_minutes",
            "total_trip_minutes",
            "trip_status",
            "trip_note",
            "reason",
            "checkout_time",
            "checkin_time",
            "allowed_minutes",
            "overdue_minutes",
            "alert_time"
        };

        public EmailTemplateService(
            IRepository<EmailTemplate> templateRepository,
            IOptions<EmailSettings> emailSettings,
            ILogger<EmailTemplateService> logger)
        {
            _templateRepository = templateRepository;
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<EmailTemplateDto>> GetAllTemplatesAsync(CancellationToken cancellationToken = default)
        {
            var templates = await _templateRepository.GetAllAsync(cancellationToken);
            return templates.Select(MapToDto).ToList();
        }

        public async Task<EmailTemplateDto?> GetTemplateByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var template = await _templateRepository.GetByIdAsync(id, cancellationToken);
            return template == null ? null : MapToDto(template);
        }

        public async Task<EmailTemplateDto?> GetTemplateByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var template = await _templateRepository.FindOneAsync(t => t.TemplateCode == code && !t.IsDeleted, cancellationToken);
            return template == null ? null : MapToDto(template);
        }

        public async Task<EmailTemplateDto> CreateTemplateAsync(CreateEmailTemplateRequest request, CancellationToken cancellationToken = default)
        {
            var existing = await _templateRepository.FindOneAsync(t => t.TemplateCode == request.TemplateCode && !t.IsDeleted, cancellationToken);
            if (existing != null)
            {
                throw new ConflictException($"Mẫu email với mã '{request.TemplateCode}' đã tồn tại.");
            }

            var entity = new EmailTemplate
            {
                TemplateCode = request.TemplateCode.Trim().ToUpperInvariant(),
                TemplateName = request.TemplateName.Trim(),
                AlertType = string.IsNullOrWhiteSpace(request.AlertType) ? request.TemplateCode : request.AlertType.Trim(),
                SubjectTemplate = request.SubjectTemplate.Trim(),
                BodyTemplate = request.BodyTemplate,
                Recipients = request.Recipients?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToList() ?? new List<string>(),
                Enabled = request.Enabled,
                AvailableVariables = StandardVariables.ToList()
            };

            await _templateRepository.AddAsync(entity, cancellationToken);
            _logger.LogInformation("[EmailTemplateService] Tạo mới mẫu email thành công: {TemplateCode}", entity.TemplateCode);
            return MapToDto(entity);
        }

        public async Task<EmailTemplateDto> UpdateTemplateAsync(string id, UpdateEmailTemplateRequest request, CancellationToken cancellationToken = default)
        {
            var entity = await _templateRepository.GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                throw new NotFoundException($"Không tìm thấy mẫu email với ID '{id}'.");
            }

            if (!string.IsNullOrWhiteSpace(request.TemplateName))
            {
                entity.TemplateName = request.TemplateName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.AlertType))
            {
                entity.AlertType = request.AlertType.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.SubjectTemplate))
            {
                entity.SubjectTemplate = request.SubjectTemplate.Trim();
            }

            if (request.BodyTemplate != null)
            {
                entity.BodyTemplate = request.BodyTemplate;
            }

            if (request.Recipients != null)
            {
                entity.Recipients = request.Recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToList();
            }

            if (request.Enabled.HasValue)
            {
                entity.Enabled = request.Enabled.Value;
            }

            entity.UpdatedAt = DateTime.Now;

            await _templateRepository.UpdateAsync(entity, cancellationToken);
            _logger.LogInformation("[EmailTemplateService] Cập nhật mẫu email thành công: {TemplateCode}", entity.TemplateCode);
            return MapToDto(entity);
        }

        public async Task<bool> DeleteTemplateAsync(string id, CancellationToken cancellationToken = default)
        {
            var entity = await _templateRepository.GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                throw new NotFoundException($"Không tìm thấy mẫu email với ID '{id}'.");
            }

            var result = await _templateRepository.DeleteAsync(id, softDelete: true, cancellationToken);
            _logger.LogInformation("[EmailTemplateService] Xóa mẫu email: {TemplateCode} (Success: {Result})", entity.TemplateCode, result);
            return result;
        }

        public async Task<PreviewEmailTemplateResponse> PreviewTemplateAsync(PreviewEmailTemplateRequest request, CancellationToken cancellationToken = default)
        {
            string? subjectTemplate = (string.IsNullOrWhiteSpace(request.SubjectTemplate) || string.Equals(request.SubjectTemplate.Trim(), "string", StringComparison.OrdinalIgnoreCase))
                ? null
                : request.SubjectTemplate.Trim();

            string? bodyTemplate = (string.IsNullOrWhiteSpace(request.BodyTemplate) || string.Equals(request.BodyTemplate.Trim(), "string", StringComparison.OrdinalIgnoreCase))
                ? null
                : request.BodyTemplate.Trim();

            var templateCode = (string.IsNullOrWhiteSpace(request.TemplateCode) || string.Equals(request.TemplateCode.Trim(), "string", StringComparison.OrdinalIgnoreCase))
                ? "TRIP_DESTINATION_MISSING"
                : request.TemplateCode.Trim();

            var template = await _templateRepository.FindOneAsync(t => t.TemplateCode == templateCode && !t.IsDeleted, cancellationToken);
            if (template != null)
            {
                subjectTemplate ??= template.SubjectTemplate;
                bodyTemplate ??= template.BodyTemplate;
            }
            else
            {
                subjectTemplate ??= "⚠ [CẢNH BÁO XE QUÁ HẠN] Xe {{vehicle_code}} chưa đến {{destination_gate}}";
                bodyTemplate ??= "<p>Xem trước mẫu email cảnh báo quá hạn cho xe {{vehicle_code}}.</p>";
            }

            var sampleVars = GetDefaultSampleVariables();
            if (request.CustomVariables != null)
            {
                foreach (var kvp in request.CustomVariables)
                {
                    sampleVars[kvp.Key] = kvp.Value;
                }
            }

            var renderedSubject = RenderTemplate(subjectTemplate, sampleVars);
            var renderedBody = RenderTemplate(bodyTemplate, sampleVars);

            return new PreviewEmailTemplateResponse
            {
                RenderedSubject = renderedSubject,
                RenderedBody = renderedBody,
                VariablesUsed = sampleVars
            };
        }

        public async Task<TestSendEmailResponse> TestSendEmailAsync(TestSendEmailRequest request, CancellationToken cancellationToken = default)
        {
            var recipient = string.IsNullOrWhiteSpace(request.RecipientEmail) 
                ? (_emailSettings.HrRecipients?.Split(new[] { ';', ',' }).FirstOrDefault() ?? "phanductuan07@gmail.com") 
                : request.RecipientEmail.Trim();

            string? subjectTemplate = (string.IsNullOrWhiteSpace(request.CustomSubject) || string.Equals(request.CustomSubject.Trim(), "string", StringComparison.OrdinalIgnoreCase))
                ? null 
                : request.CustomSubject.Trim();

            string? bodyTemplate = (string.IsNullOrWhiteSpace(request.CustomBody) || string.Equals(request.CustomBody.Trim(), "string", StringComparison.OrdinalIgnoreCase))
                ? null 
                : request.CustomBody.Trim();

            var templateCode = (string.IsNullOrWhiteSpace(request.TemplateCode) || string.Equals(request.TemplateCode.Trim(), "string", StringComparison.OrdinalIgnoreCase))
                ? "TRIP_DESTINATION_MISSING"
                : request.TemplateCode.Trim();

            var template = await _templateRepository.FindOneAsync(t => t.TemplateCode == templateCode && !t.IsDeleted, cancellationToken);
            if (template != null)
            {
                subjectTemplate ??= template.SubjectTemplate;
                bodyTemplate ??= template.BodyTemplate;
            }
            else
            {
                subjectTemplate ??= "⚠ [TEST CẢNH BÁO XE QUÁ HẠN] Xe {{vehicle_code}} chưa đến {{destination_gate}}";
                bodyTemplate ??= "<p>Đây là email kiểm tra kịch bản cảnh báo quá hạn cho xe <strong>{{vehicle_code}}</strong>.</p>";
            }

            var sampleVars = GetDefaultSampleVariables();
            sampleVars["sender"] = _emailSettings.FromEmail;
            if (request.CustomVariables != null)
            {
                foreach (var kvp in request.CustomVariables)
                {
                    sampleVars[kvp.Key] = kvp.Value;
                }
            }

            var finalSubject = RenderTemplate(subjectTemplate, sampleVars);
            var finalBody = RenderTemplate(bodyTemplate, sampleVars);

            try
            {
                using var client = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort)
                {
                    EnableSsl = _emailSettings.EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                if (!string.IsNullOrWhiteSpace(_emailSettings.UserName))
                {
                    client.Credentials = new NetworkCredential(_emailSettings.UserName, _emailSettings.Password);
                }

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                    Subject = finalSubject,
                    Body = finalBody,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipient);

                await client.SendMailAsync(mailMessage, cancellationToken);
                _logger.LogInformation("[EmailTemplateService] Gửi email test thành công từ {Sender} tới {Recipient}", _emailSettings.FromEmail, recipient);

                return new TestSendEmailResponse
                {
                    Success = true,
                    Message = $"Đã gửi email test thành công từ {_emailSettings.FromEmail} tới {recipient}.",
                    Sender = _emailSettings.FromEmail,
                    Recipient = recipient,
                    Subject = finalSubject
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EmailTemplateService] Lỗi khi gửi email test tới {Recipient}", recipient);
                return new TestSendEmailResponse
                {
                    Success = false,
                    Message = $"Gửi email test thất bại: {ex.Message}",
                    Sender = _emailSettings.FromEmail,
                    Recipient = recipient,
                    Subject = finalSubject
                };
            }
        }

        public IReadOnlyList<string> GetAvailableVariables(string? templateCode = null)
        {
            return StandardVariables.ToList();
        }

        public string RenderTemplate(string template, IDictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            // Regex khớp các biến định dạng: {{variable_name}} hoặc {{ variable_name }}
            var regex = new Regex(@"\{\{\s*([a-zA-Z0-9_\-]+)\s*\}\}", RegexOptions.Compiled);
            return regex.Replace(template, match =>
            {
                var key = match.Groups[1].Value;
                if (variables != null && variables.TryGetValue(key, out var val))
                {
                    return val ?? string.Empty;
                }
                return match.Value; // Giữ nguyên nếu không tìm thấy biến thay thế
            });
        }

        private static Dictionary<string, string> GetDefaultSampleVariables()
        {
            var now = DateTime.Now;
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["vehicle_code"] = "XE-01",
                ["plate_number"] = "29B-99999",
                ["tag_code"] = "E280116060000204",
                ["tag_epc"] = "E280116060000204",
                ["origin_gate"] = "Cổng 1",
                ["origin_gate_code"] = "C1",
                ["destination_gate"] = "Cổng 2",
                ["destination_gate_code"] = "C2",
                ["checkout_origin_time"] = now.AddMinutes(-25).ToString("HH:mm:ss dd/MM/yyyy"),
                ["checkin_dest_time"] = now.AddMinutes(-21).ToString("HH:mm:ss dd/MM/yyyy"),
                ["checkout_dest_time"] = now.AddMinutes(-6).ToString("HH:mm:ss dd/MM/yyyy"),
                ["checkin_origin_time"] = now.ToString("HH:mm:ss dd/MM/yyyy"),
                ["deadline_checkin_dest"] = now.AddMinutes(-20).ToString("HH:mm:ss dd/MM/yyyy"),
                ["deadline_checkout_dest"] = now.AddMinutes(-1).ToString("HH:mm:ss dd/MM/yyyy"),
                ["deadline_checkin_origin"] = now.AddMinutes(-1).ToString("HH:mm:ss dd/MM/yyyy"),
                ["travel_to_dest_minutes"] = "4.0",
                ["stay_at_dest_minutes"] = "15.0",
                ["travel_back_minutes"] = "6.0",
                ["total_trip_minutes"] = "25.0",
                ["trip_status"] = "InTransitToDest",
                ["trip_note"] = "Kiểm tra tiến độ di chuyển của phương tiện",
                ["reason"] = "Xe quá thời gian di chuyển quy định",
                ["checkout_time"] = now.AddMinutes(-8).ToString("HH:mm:ss dd/MM/yyyy"),
                ["checkin_time"] = now.AddMinutes(-23).ToString("HH:mm:ss dd/MM/yyyy"),
                ["allowed_minutes"] = "5",
                ["overdue_minutes"] = "3",
                ["alert_time"] = now.ToString("HH:mm:ss dd/MM/yyyy")
            };
        }

        private static EmailTemplateDto MapToDto(EmailTemplate entity)
        {
            return new EmailTemplateDto
            {
                Id = entity.Id,
                TemplateCode = entity.TemplateCode,
                TemplateName = entity.TemplateName,
                AlertType = entity.AlertType,
                SubjectTemplate = entity.SubjectTemplate,
                BodyTemplate = entity.BodyTemplate,
                Recipients = entity.Recipients ?? new List<string>(),
                Enabled = entity.Enabled,
                AvailableVariables = entity.AvailableVariables ?? StandardVariables.ToList(),
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
