using HPParking.Api.Configuration;
using HPParking.Api.Services.Interfaces;
using HPParking.Core.Interfaces;
using HPParking.Core.Models.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace HPParking.Api.Services.Implementations
{
    /// <summary>
    /// Triển khai dịch vụ gửi email cảnh báo tự động đến bộ phận nhân sự và điều phối xe
    /// Tích hợp Email Template động từ Database với các placeholder {{...}}
    /// </summary>
    public class EmailAlertService : IEmailAlertService
    {
        private readonly EmailSettings _emailSettings;
        private readonly IRepository<EmailTemplate> _templateRepository;
        private readonly IRepository<Vehicle> _vehicleRepository;
        private readonly IEmailTemplateService _templateService;
        private readonly ILogger<EmailAlertService> _logger;

        public EmailAlertService(
            IOptions<EmailSettings> emailSettings,
            IRepository<EmailTemplate> templateRepository,
            IRepository<Vehicle> vehicleRepository,
            IEmailTemplateService templateService,
            ILogger<EmailAlertService> logger)
        {
            _emailSettings = emailSettings.Value;
            _templateRepository = templateRepository;
            _vehicleRepository = vehicleRepository;
            _templateService = templateService;
            _logger = logger;
        }

        private Dictionary<string, string> BuildTripVariables(VehicleTrip trip, Vehicle? vehicle, double allowedMinutes, double overdue, string? reason = null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Thông tin phương tiện
                ["vehicle_code"] = trip.PlateNumber,
                ["plate_number"] = trip.PlateNumber,
                ["tag_code"] = vehicle?.TagEpc ?? "Chưa gán thẻ",
                ["tag_epc"] = vehicle?.TagEpc ?? "Chưa gán thẻ",

                // Thông tin các cổng
                ["origin_gate"] = trip.OriginGateCode,
                ["origin_gate_code"] = trip.OriginGateCode,
                ["destination_gate"] = trip.DestinationGateCode,
                ["destination_gate_code"] = trip.DestinationGateCode,

                // Chi tiết 4 mốc CHECK-IN / CHECK-OUT
                ["checkout_origin_time"] = trip.CheckOutOriginTime.ToString("HH:mm:ss dd/MM/yyyy"),
                ["checkin_dest_time"] = trip.CheckInDestinationTime?.ToString("HH:mm:ss dd/MM/yyyy") ?? "Chưa ghi nhận",
                ["checkout_dest_time"] = trip.CheckOutDestinationTime?.ToString("HH:mm:ss dd/MM/yyyy") ?? "Chưa ghi nhận",
                ["checkin_origin_time"] = trip.CheckInOriginTime?.ToString("HH:mm:ss dd/MM/yyyy") ?? "Chưa ghi nhận",

                // Hạn chót từng mốc
                ["deadline_checkin_dest"] = trip.DeadlineCheckInDest.ToString("HH:mm:ss dd/MM/yyyy"),
                ["deadline_checkout_dest"] = trip.DeadlineCheckOutDest?.ToString("HH:mm:ss dd/MM/yyyy") ?? "N/A",
                ["deadline_checkin_origin"] = trip.DeadlineCheckInOrigin?.ToString("HH:mm:ss dd/MM/yyyy") ?? "N/A",

                // Thời lượng thực tế từng chặng (phút)
                ["travel_to_dest_minutes"] = trip.TravelToDestMinutes?.ToString("0.##") ?? "N/A",
                ["stay_at_dest_minutes"] = trip.StayAtDestMinutes?.ToString("0.##") ?? "N/A",
                ["travel_back_minutes"] = trip.TravelBackMinutes?.ToString("0.##") ?? "N/A",
                ["total_trip_minutes"] = trip.TotalTripMinutes?.ToString("0.##") ?? "N/A",

                // Trạng thái và ghi chú
                ["trip_status"] = trip.Status.ToString(),
                ["trip_note"] = trip.Note ?? string.Empty,
                ["reason"] = reason ?? string.Empty,

                // Tương thích ngược với các template cũ
                ["checkout_time"] = trip.CheckOutDestinationTime?.ToString("HH:mm:ss dd/MM/yyyy") 
                    ?? trip.CheckOutOriginTime.ToString("HH:mm:ss dd/MM/yyyy"),
                ["checkin_time"] = trip.CheckInDestinationTime?.ToString("HH:mm:ss dd/MM/yyyy") ?? "Chưa ghi nhận",
                ["allowed_minutes"] = allowedMinutes > 0 ? allowedMinutes.ToString("0") : "5",
                ["overdue_minutes"] = overdue.ToString("0.#"),
                ["alert_time"] = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")
            };
        }

        public async Task SendMissingCheckInDestAlertAsync(VehicleTrip trip, CancellationToken cancellationToken = default)
        {
            var overdue = Math.Round((DateTime.Now - trip.DeadlineCheckInDest).TotalMinutes, 1);
            var allowedMinutes = Math.Round((trip.DeadlineCheckInDest - trip.CheckOutOriginTime).TotalMinutes, 0);

            var template = await _templateRepository.FindOneAsync(t => t.TemplateCode == "TRIP_DESTINATION_MISSING" && !t.IsDeleted, cancellationToken);
            if (template != null && !template.Enabled)
            {
                _logger.LogInformation("[EmailAlertService] Mẫu email TRIP_DESTINATION_MISSING đang bị TẮT (Enabled = false). Bỏ qua gửi mail cảnh báo.");
                return;
            }

            var vehicle = !string.IsNullOrWhiteSpace(trip.VehicleId) 
                ? await _vehicleRepository.GetByIdAsync(trip.VehicleId, cancellationToken) 
                : null;

            var variables = BuildTripVariables(trip, vehicle, allowedMinutes, overdue);

            string subject;
            string body;
            List<string> recipients;

            if (template != null)
            {
                subject = _templateService.RenderTemplate(template.SubjectTemplate, variables);
                body = _templateService.RenderTemplate(template.BodyTemplate, variables);
                recipients = template.Recipients != null && template.Recipients.Count > 0 
                    ? template.Recipients 
                    : GetDefaultRecipients();
            }
            else
            {
                subject = $"[CẢNH BÁO XE QUÁ HẠN] Xe {trip.PlateNumber} chưa CHECK_IN tại {trip.DestinationGateCode}";
                body = BuildFallbackBody(
                    title: "⚠️ CẢNH BÁO XE CHƯA ĐẾN CỔNG ĐÍCH",
                    color: "#d9534f",
                    trip: trip,
                    vehicle: vehicle,
                    statusText: $"Đã quá hạn {overdue} phút nhưng chưa ghi nhận tín hiệu CHECK IN tại cổng đích.",
                    currentStage: 1,
                    overdue: overdue);
                recipients = GetDefaultRecipients();
            }

            await SendEmailAsync(subject, body, recipients, cancellationToken);
        }

        public async Task SendOverstayDestAlertAsync(VehicleTrip trip, CancellationToken cancellationToken = default)
        {
            var overdue = trip.DeadlineCheckOutDest.HasValue 
                ? Math.Round((DateTime.Now - trip.DeadlineCheckOutDest.Value).TotalMinutes, 1) 
                : 0;

            var allowedMinutes = (trip.CheckInDestinationTime.HasValue && trip.DeadlineCheckOutDest.HasValue)
                ? Math.Round((trip.DeadlineCheckOutDest.Value - trip.CheckInDestinationTime.Value).TotalMinutes, 0)
                : 20;

            var template = await _templateRepository.FindOneAsync(t => t.TemplateCode == "TRIP_DESTINATION_TIMEOUT" && !t.IsDeleted, cancellationToken);
            if (template != null && !template.Enabled)
            {
                _logger.LogInformation("[EmailAlertService] Mẫu email TRIP_DESTINATION_TIMEOUT đang bị TẮT (Enabled = false). Bỏ qua gửi mail cảnh báo.");
                return;
            }

            var vehicle = !string.IsNullOrWhiteSpace(trip.VehicleId) 
                ? await _vehicleRepository.GetByIdAsync(trip.VehicleId, cancellationToken) 
                : null;

            var variables = BuildTripVariables(trip, vehicle, allowedMinutes, overdue);

            string subject;
            string body;
            List<string> recipients;

            if (template != null)
            {
                subject = _templateService.RenderTemplate(template.SubjectTemplate, variables);
                body = _templateService.RenderTemplate(template.BodyTemplate, variables);
                recipients = template.Recipients != null && template.Recipients.Count > 0 
                    ? template.Recipients 
                    : GetDefaultRecipients();
            }
            else
            {
                subject = $"[CẢNH BÁO XE QUÁ HẠN] Xe {trip.PlateNumber} dừng quá giờ tại {trip.DestinationGateCode}";
                body = BuildFallbackBody(
                    title: "⚠️ CẢNH BÁO XE DỪNG QUÁ GIỜ TẠI CỔNG ĐÍCH",
                    color: "#f0ad4e",
                    trip: trip,
                    vehicle: vehicle,
                    statusText: $"Đã quá hạn bốc dỡ {overdue} phút mà chưa ghi nhận tín hiệu CHECK OUT rời cổng.",
                    currentStage: 2,
                    overdue: overdue);
                recipients = GetDefaultRecipients();
            }

            await SendEmailAsync(subject, body, recipients, cancellationToken);
        }

        public async Task SendMissingCheckInOriginAlertAsync(VehicleTrip trip, CancellationToken cancellationToken = default)
        {
            var overdue = trip.DeadlineCheckInOrigin.HasValue 
                ? Math.Round((DateTime.Now - trip.DeadlineCheckInOrigin.Value).TotalMinutes, 1) 
                : 0;

            var allowedMinutes = (trip.CheckOutDestinationTime.HasValue && trip.DeadlineCheckInOrigin.HasValue)
                ? Math.Round((trip.DeadlineCheckInOrigin.Value - trip.CheckOutDestinationTime.Value).TotalMinutes, 0)
                : 5;

            var template = await _templateRepository.FindOneAsync(t => t.TemplateCode == "TRIP_RETURN_TIMEOUT" && !t.IsDeleted, cancellationToken);
            if (template != null && !template.Enabled)
            {
                _logger.LogInformation("[EmailAlertService] Mẫu email TRIP_RETURN_TIMEOUT đang bị TẮT (Enabled = false). Bỏ qua gửi mail cảnh báo.");
                return;
            }

            var vehicle = !string.IsNullOrWhiteSpace(trip.VehicleId) 
                ? await _vehicleRepository.GetByIdAsync(trip.VehicleId, cancellationToken) 
                : null;

            var variables = BuildTripVariables(trip, vehicle, allowedMinutes, overdue);

            string subject;
            string body;
            List<string> recipients;

            if (template != null)
            {
                subject = _templateService.RenderTemplate(template.SubjectTemplate, variables);
                body = _templateService.RenderTemplate(template.BodyTemplate, variables);
                recipients = template.Recipients != null && template.Recipients.Count > 0 
                    ? template.Recipients 
                    : GetDefaultRecipients();
            }
            else
            {
                subject = $"[CẢNH BÁO XE QUÁ HẠN] Xe {trip.PlateNumber} chưa quay về {trip.OriginGateCode}";
                body = BuildFallbackBody(
                    title: "⚠️ CẢNH BÁO XE CHƯA VỀ LẠI CỔNG XUẤT PHÁT",
                    color: "#d9534f",
                    trip: trip,
                    vehicle: vehicle,
                    statusText: $"Đã quá hạn {overdue} phút chưa thấy CHECK IN quay về cổng xuất phát.",
                    currentStage: 3,
                    overdue: overdue);
                recipients = GetDefaultRecipients();
            }

            await SendEmailAsync(subject, body, recipients, cancellationToken);
        }

        public async Task SendAbnormalTripAlertAsync(VehicleTrip trip, string reason, CancellationToken cancellationToken = default)
        {
            var vehicle = !string.IsNullOrWhiteSpace(trip.VehicleId) 
                ? await _vehicleRepository.GetByIdAsync(trip.VehicleId, cancellationToken) 
                : null;

            var template = await _templateRepository.FindOneAsync(t => t.TemplateCode == "TRIP_ABNORMAL" && !t.IsDeleted, cancellationToken);
            if (template != null && !template.Enabled)
            {
                _logger.LogInformation("[EmailAlertService] Mẫu email TRIP_ABNORMAL đang bị TẮT (Enabled = false). Bỏ qua gửi mail.");
                return;
            }

            var variables = BuildTripVariables(trip, vehicle, 0, 0, reason);

            string subject;
            string body;
            List<string> recipients;

            if (template != null)
            {
                subject = _templateService.RenderTemplate(template.SubjectTemplate, variables);
                body = _templateService.RenderTemplate(template.BodyTemplate, variables);
                recipients = template.Recipients != null && template.Recipients.Count > 0 
                    ? template.Recipients 
                    : GetDefaultRecipients();
            }
            else
            {
                subject = $"[CẢNH BÁO BẤT THƯỜNG] Xe {trip.PlateNumber} về {trip.OriginGateCode} KHÔNG CÓ CHECK-OUT tại {trip.DestinationGateCode}";
                
                body = $@"
<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 700px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 6px; overflow: hidden;'>
    <div style='background-color: #d9534f; color: white; padding: 14px 20px;'>
        <h2 style='margin: 0; font-size: 18px;'>⚠️ PHÁT HIỆN HÀNH TRÌNH BẤT THƯỜNG - SAI QUY TRÌNH KIỂM SOÁT</h2>
        <p style='margin: 4px 0 0 0; font-size: 12px; opacity: 0.9;'>Hệ thống Giám sát Hành trình Xe nâng UHF - HPParking</p>
    </div>
    <div style='padding: 18px; background-color: #ffffff;'>
        <p style='margin-top: 0;'>Kính gửi Bộ phận Nhân sự & Quản đốc Điều phối,</p>
        <p>Hệ thống phát hiện phương tiện có dấu hiệu <strong>không tuân thủ quy trình bốc dỡ hàng hoặc né tránh kiểm soát cổng</strong>:</p>
        
        <table style='width: 100%; border-collapse: collapse; margin-bottom: 15px;'>
            <tr><td style='padding: 5px 0; width: 200px;'><strong>Biển số xe:</strong></td><td><span style='color: #d9534f; font-weight: bold; font-size: 16px;'>{trip.PlateNumber}</span></td></tr>
            <tr><td style='padding: 5px 0;'><strong>Mã UHF / EPC:</strong></td><td><code>{vehicle?.TagEpc ?? "Chưa gán thẻ"}</code></td></tr>
            <tr><td style='padding: 5px 0;'><strong>Cổng xuất phát (Cổng gán):</strong></td><td><strong>{trip.OriginGateCode}</strong></td></tr>
            <tr><td style='padding: 5px 0;'><strong>Cổng phụ (Nơi bốc dỡ):</strong></td><td><strong>{trip.DestinationGateCode}</strong></td></tr>
        </table>

        <h4 style='margin: 12px 0 6px 0; color: #333; border-bottom: 1px solid #eee; padding-bottom: 4px;'>CHI TIẾT 4 MỐC CHECK-IN / CHECK-OUT:</h4>
        <table style='width: 100%; border-collapse: collapse; background-color: #fbfbfb; border: 1px solid #eee; font-size: 13px;'>
            <thead>
                <tr style='background-color: #f0f0f0; border-bottom: 2px solid #ddd;'>
                    <th style='padding: 8px 10px; text-align: left;'>Mốc kiểm soát</th>
                    <th style='padding: 8px 10px; text-align: left;'>Cổng</th>
                    <th style='padding: 8px 10px; text-align: left;'>Thời gian ghi nhận</th>
                    <th style='padding: 8px 10px; text-align: left;'>Trạng thái</th>
                </tr>
            </thead>
            <tbody>
                <tr style='border-bottom: 1px solid #eee;'>
                    <td style='padding: 8px 10px;'>1. CHECK_OUT Cổng xuất phát</td>
                    <td style='padding: 8px 10px;'>{trip.OriginGateCode}</td>
                    <td style='padding: 8px 10px;'>{trip.CheckOutOriginTime:HH:mm:ss dd/MM/yyyy}</td>
                    <td style='padding: 8px 10px;'><span style='color: green; font-weight: bold;'>Đã xuất phát</span></td>
                </tr>
                <tr style='border-bottom: 1px solid #eee;'>
                    <td style='padding: 8px 10px;'>2. CHECK_IN Cổng phụ</td>
                    <td style='padding: 8px 10px;'>{trip.DestinationGateCode}</td>
                    <td style='padding: 8px 10px;'>{trip.CheckInDestinationTime?.ToString("HH:mm:ss dd/MM/yyyy") ?? "N/A"}</td>
                    <td style='padding: 8px 10px;'><span style='color: green; font-weight: bold;'>Đã đến ({trip.TravelToDestMinutes}p)</span></td>
                </tr>
                <tr style='border-bottom: 1px solid #eee; background-color: #ffeef0;'>
                    <td style='padding: 8px 10px;'>3. CHECK_OUT Cổng phụ</td>
                    <td style='padding: 8px 10px;'>{trip.DestinationGateCode}</td>
                    <td style='padding: 8px 10px;'><strong style='color: red;'>KHÔNG CÓ TÍN HIỆU</strong></td>
                    <td style='padding: 8px 10px;'><span style='color: red; font-weight: bold;'>BỎ QUA / NÉ CHECK-OUT</span></td>
                </tr>
                <tr>
                    <td style='padding: 8px 10px;'>4. CHECK_IN về Cổng xuất phát</td>
                    <td style='padding: 8px 10px;'>{trip.OriginGateCode}</td>
                    <td style='padding: 8px 10px;'><strong>{trip.CheckInOriginTime?.ToString("HH:mm:ss dd/MM/yyyy") ?? "N/A"}</strong></td>
                    <td style='padding: 8px 10px;'><span style='color: orange; font-weight: bold;'>Đột ngột xuất hiện</span></td>
                </tr>
            </tbody>
        </table>

        <div style='margin-top: 14px; padding: 10px 14px; background-color: #fff3cd; border-left: 4px solid #ffc107; color: #856404; font-size: 13px;'>
            <strong>Lý do cảnh báo:</strong> {reason}
            <br/><strong>Tổng thời gian từ khi đi đến khi về:</strong> {trip.TotalTripMinutes} phút.
            <br/><em>Kính đề nghị Quản đốc / Nhân sự trích xuất Camera tại cổng {trip.DestinationGateCode} và đối chiếu phiếu bốc dỡ hàng thực tế của xe.</em>
        </div>
        <hr style='border: none; border-top: 1px solid #eee; margin: 18px 0 12px 0;' />
        <p style='margin: 0; font-size: 12px; color: #888;'>Hệ thống HPParking Alert Service • Tự động gửi lúc {DateTime.Now:HH:mm:ss dd/MM/yyyy}</p>
    </div>
</div>";
                recipients = GetDefaultRecipients();
            }

            await SendEmailAsync(subject, body, recipients, cancellationToken);
        }

        private List<string> GetDefaultRecipients()
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.HrRecipients))
            {
                return new List<string> { "phanductuan07@gmail.com" };
            }

            return _emailSettings.HrRecipients
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .ToList();
        }

        private string BuildFallbackBody(
            string title,
            string color,
            VehicleTrip trip,
            Vehicle? vehicle,
            string statusText,
            int currentStage,
            double overdue)
        {
            // Trạng thái mốc 2: CHECK_IN Cổng phụ
            string inDestText = trip.CheckInDestinationTime?.ToString("HH:mm:ss dd/MM/yyyy") 
                ?? (currentStage == 1 ? "<strong style='color: red;'>Chưa ghi nhận</strong>" : "Chưa có");
            string inDestStatus = trip.CheckInDestinationTime.HasValue 
                ? $"<span style='color: green;'>Đã vào ({trip.TravelToDestMinutes}p)</span>" 
                : (currentStage == 1 ? $"<strong style='color: {color};'>QUÁ HẠN {overdue}p</strong>" : "--");

            // Trạng thái mốc 3: CHECK_OUT Cổng phụ
            string outDestText = trip.CheckOutDestinationTime?.ToString("HH:mm:ss dd/MM/yyyy") 
                ?? (currentStage == 2 ? "<strong style='color: orange;'>Chưa rời cổng</strong>" : "Chưa có");
            string outDestDeadline = trip.DeadlineCheckOutDest?.ToString("HH:mm:ss dd/MM/yyyy") ?? "N/A";
            string outDestStatus = trip.CheckOutDestinationTime.HasValue
                ? $"<span style='color: green;'>Đã rời ({trip.StayAtDestMinutes}p)</span>"
                : (currentStage == 2 ? $"<strong style='color: {color};'>QUÁ HẠN {overdue}p</strong>" : "--");

            // Trạng thái mốc 4: CHECK_IN Cổng xuất phát
            string inOriginText = trip.CheckInOriginTime?.ToString("HH:mm:ss dd/MM/yyyy") 
                ?? (currentStage == 3 ? "<strong style='color: red;'>Chưa quay về</strong>" : "Chưa có");
            string inOriginDeadline = trip.DeadlineCheckInOrigin?.ToString("HH:mm:ss dd/MM/yyyy") ?? "N/A";
            string inOriginStatus = trip.CheckInOriginTime.HasValue
                ? $"<span style='color: green;'>Đã về ({trip.TravelBackMinutes}p)</span>"
                : (currentStage == 3 ? $"<strong style='color: {color};'>QUÁ HẠN {overdue}p</strong>" : "--");

            return $@"
<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 700px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 6px; overflow: hidden;'>
    <div style='background-color: {color}; color: white; padding: 14px 20px;'>
        <h2 style='margin: 0; font-size: 18px;'>{title}</h2>
        <p style='margin: 4px 0 0 0; font-size: 12px; opacity: 0.9;'>Hệ thống Giám sát Hành trình Xe nâng UHF - HPParking</p>
    </div>
    <div style='padding: 18px; background-color: #ffffff;'>
        <p style='margin-top: 0;'>Kính gửi Bộ phận Nhân sự & Quản đốc Điều phối,</p>
        <p>Hệ thống giám sát hành trình UHF ghi nhận phương tiện di chuyển quá thời hạn quy định:</p>
        
        <table style='width: 100%; border-collapse: collapse; margin-bottom: 12px;'>
            <tr><td style='padding: 4px 0; width: 180px;'><strong>Biển số xe:</strong></td><td><span style='color: {color}; font-weight: bold; font-size: 15px;'>{trip.PlateNumber}</span></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Mã UHF / EPC:</strong></td><td><code>{vehicle?.TagEpc ?? "Chưa gán thẻ"}</code></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Cổng xuất phát:</strong></td><td><strong>{trip.OriginGateCode}</strong></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Cổng đích cần đến:</strong></td><td><strong>{trip.DestinationGateCode}</strong></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Thời gian quá hạn:</strong></td><td><strong style='color: {color}; font-size: 15px;'>{overdue} phút</strong></td></tr>
        </table>

        <h4 style='margin: 12px 0 6px 0; color: #333; border-bottom: 1px solid #eee; padding-bottom: 4px;'>DÒNG THỜI GIAN KIỂM SOÁT (CHECK-IN / CHECK-OUT):</h4>
        <table style='width: 100%; border-collapse: collapse; background-color: #fbfbfb; border: 1px solid #eee; font-size: 13px;'>
            <thead>
                <tr style='background-color: #f0f0f0; border-bottom: 2px solid #ddd;'>
                    <th style='padding: 8px 10px; text-align: left;'>Mốc kiểm soát</th>
                    <th style='padding: 8px 10px; text-align: left;'>Cổng</th>
                    <th style='padding: 8px 10px; text-align: left;'>Thời gian ghi nhận</th>
                    <th style='padding: 8px 10px; text-align: left;'>Hạn chót quy định</th>
                    <th style='padding: 8px 10px; text-align: left;'>Trạng thái</th>
                </tr>
            </thead>
            <tbody>
                <tr style='border-bottom: 1px solid #eee;'>
                    <td style='padding: 8px 10px;'>1. CHECK_OUT Cổng xuất phát</td>
                    <td style='padding: 8px 10px;'>{trip.OriginGateCode}</td>
                    <td style='padding: 8px 10px;'>{trip.CheckOutOriginTime:HH:mm:ss dd/MM/yyyy}</td>
                    <td style='padding: 8px 10px;'>--</td>
                    <td style='padding: 8px 10px;'><span style='color: green; font-weight: bold;'>Đã xuất phát</span></td>
                </tr>
                <tr style='border-bottom: 1px solid #eee;'>
                    <td style='padding: 8px 10px;'>2. CHECK_IN Cổng đích</td>
                    <td style='padding: 8px 10px;'>{trip.DestinationGateCode}</td>
                    <td style='padding: 8px 10px;'>{inDestText}</td>
                    <td style='padding: 8px 10px;'>{trip.DeadlineCheckInDest:HH:mm:ss dd/MM/yyyy}</td>
                    <td style='padding: 8px 10px;'>{inDestStatus}</td>
                </tr>
                <tr style='border-bottom: 1px solid #eee;'>
                    <td style='padding: 8px 10px;'>3. CHECK_OUT Cổng đích</td>
                    <td style='padding: 8px 10px;'>{trip.DestinationGateCode}</td>
                    <td style='padding: 8px 10px;'>{outDestText}</td>
                    <td style='padding: 8px 10px;'>{outDestDeadline}</td>
                    <td style='padding: 8px 10px;'>{outDestStatus}</td>
                </tr>
                <tr>
                    <td style='padding: 8px 10px;'>4. CHECK_IN về Cổng xuất phát</td>
                    <td style='padding: 8px 10px;'>{trip.OriginGateCode}</td>
                    <td style='padding: 8px 10px;'>{inOriginText}</td>
                    <td style='padding: 8px 10px;'>{inOriginDeadline}</td>
                    <td style='padding: 8px 10px;'>{inOriginStatus}</td>
                </tr>
            </tbody>
        </table>

        <div style='margin-top: 14px; padding: 10px 14px; background-color: #fff3cd; border-left: 4px solid #ffc107; color: #856404; font-size: 13px;'>
            <strong>Trạng thái:</strong> {statusText}
            <br/><em>Kính đề nghị Quản đốc / Nhân sự kiểm tra vị trí xe trên tuyến đường nội bộ hoặc camera khu vực cổng.</em>
        </div>
        <hr style='border: none; border-top: 1px solid #eee; margin: 18px 0 12px 0;' />
        <p style='margin: 0; font-size: 12px; color: #888;'>Hệ thống HPParking Alert Service • Tự động gửi lúc {DateTime.Now:HH:mm:ss dd/MM/yyyy}</p>
    </div>
</div>";
        }

        private async Task SendEmailAsync(string subject, string htmlBody, List<string> recipients, CancellationToken cancellationToken)
        {
            if (recipients == null || recipients.Count == 0 || string.IsNullOrWhiteSpace(_emailSettings.SmtpHost))
            {
                _logger.LogWarning("[EmailAlertService] Cấu hình SMTP hoặc danh sách email người nhận trống. Bỏ qua gửi email thực tế. Tiêu đề: {Subject}", subject);
                return;
            }

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
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                foreach (var r in recipients)
                {
                    mailMessage.To.Add(r);
                }

                await client.SendMailAsync(mailMessage, cancellationToken);
                _logger.LogInformation("[EmailAlertService] Đã gửi email cảnh báo thành công: '{Subject}' từ {Sender} tới [{Recipients}]", 
                    subject, _emailSettings.FromEmail, string.Join(", ", recipients));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EmailAlertService] Gửi email cảnh báo thất bại: {Subject}", subject);
            }
        }
    }
}
