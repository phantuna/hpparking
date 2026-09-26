using HPParking.Core.Interfaces;
using HPParking.Core.Models.Entities;
using HPParking.Core.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HPParking.Api.Data
{
    public class DbSeeder
    {
        public static async Task SeedAdminUserAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger = scope.ServiceProvider.GetService<ILogger<DbSeeder>>();

            try
            {
                // 1. Kiểm tra xem đã có bất kỳ tài khoản Admin nào tồn tại trong hệ thống chưa
                var hasAdmin = await userRepo.ExistsAsync(u => u.Role == UserRole.Admin);
                if (hasAdmin)
                {
                    logger?.LogInformation("Đã tồn tại tài khoản Quản trị viên trong hệ thống. Bỏ qua bước khởi tạo (DbSeeder).");
                    return;
                }

                // 2. Đọc thông tin cấu hình Admin ban đầu (mặc định admin / admin123)
                var defaultUsername = config["AdminSeeder:DefaultUsername"] ?? "admin";
                var defaultPassword = config["AdminSeeder:DefaultPassword"] ?? "admin123";
                var defaultFullName = config["AdminSeeder:DefaultFullName"] ?? "System Administrator";

                var adminUser = new User
                {
                    Username = defaultUsername,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                    FullName = defaultFullName,
                    Role = UserRole.Admin,
                    IsActive = true
                };

                await userRepo.AddAsync(adminUser);
                logger?.LogInformation("Khởi tạo thành công tài khoản Quản trị viên mặc định '{Username}' (Role: Admin).", defaultUsername);

                // 3. Khởi tạo tài khoản Nhân sự phanductuan07 nếu chưa tồn tại
                var hasHr = await userRepo.ExistsAsync(u => u.Username == "phanductuan07" || u.Email == "phanductuan07@gmail.com");
                if (!hasHr)
                {
                    var hrUser = new User
                    {
                        Username = "phanductuan07",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("tuan123"),
                        FullName = "Phan Đức Tuấn (HR)",
                        Email = "phanductuan07@gmail.com",
                        Role = UserRole.HR,
                        IsActive = true
                    };
                    await userRepo.AddAsync(hrUser);
                    logger?.LogInformation("Khởi tạo thành công tài khoản Nhân sự 'phanductuan07' (Role: HR, Email: phanductuan07@gmail.com).");
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Không thể kết nối hoặc khởi tạo dữ liệu ban đầu từ DbSeeder: {Message}. Tiếp tục khởi động ứng dụng.", ex.Message);
            }
        }

        /// <summary>
        /// Khởi tạo các mẫu Email Cảnh báo vi phạm hành trình xe nâng (ADR 0037)
        /// Người nhận mặc định: phanductuan07@gmail.com
        /// </summary>
        public static async Task SeedEmailTemplatesAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var templateRepo = scope.ServiceProvider.GetRequiredService<IRepository<EmailTemplate>>();
            var logger = scope.ServiceProvider.GetService<ILogger<DbSeeder>>();

            try
            {
                var defaultRecipients = new List<string> { "phanductuan07@gmail.com" };
                var standardVariables = new List<string>
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

                // Mẫu 1: Xe chưa CHECK_IN tại cổng đích (Quá hạn di chuyển từ Cổng xuất phát -> Cổng phụ)
                var t1 = new EmailTemplate
                {
                    TemplateCode = "TRIP_DESTINATION_MISSING",
                    TemplateName = "Cảnh báo xe quá thời gian di chuyển (chưa đến cổng đích)",
                    AlertType = "TRIP_DESTINATION_MISSING",
                    SubjectTemplate = "⚠ [CẢNH BÁO XE QUÁ HẠN] Xe {{vehicle_code}} chưa đến {{destination_gate}}",
                    BodyTemplate = @"
<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 700px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 6px; overflow: hidden;'>
    <div style='background-color: #d9534f; color: white; padding: 14px 20px;'>
        <h2 style='margin: 0; font-size: 18px;'>⚠️ CẢNH BÁO XE CHƯA ĐẾN CỔNG ĐÍCH</h2>
        <p style='margin: 4px 0 0 0; font-size: 12px; opacity: 0.9;'>Hệ thống Giám sát Hành trình Xe nâng UHF - HPParking</p>
    </div>
    <div style='padding: 18px; background-color: #ffffff;'>
        <p style='margin-top: 0;'>Kính gửi Bộ phận Nhân sự & Quản đốc Điều phối,</p>
        <p>Hệ thống ghi nhận xe <strong>{{vehicle_code}}</strong> đã CHECK_OUT tại <strong>{{origin_gate}}</strong> nhưng chưa CHECK_IN tại <strong>{{destination_gate}}</strong> trong thời gian cho phép.</p>
        
        <table style='width: 100%; border-collapse: collapse; margin-bottom: 12px;'>
            <tr><td style='padding: 4px 0; width: 180px;'><strong>Biển số xe:</strong></td><td><span style='color: #d9534f; font-weight: bold; font-size: 15px;'>{{vehicle_code}}</span></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Mã UHF / EPC:</strong></td><td><code>{{tag_code}}</code></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Cổng xuất phát:</strong></td><td><strong>{{origin_gate}}</strong></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Cổng đích cần đến:</strong></td><td><strong>{{destination_gate}}</strong></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Thời gian cho phép:</strong></td><td>{{allowed_minutes}} phút</td></tr>
            <tr><td style='padding: 4px 0;'><strong>Thời gian đã quá hạn:</strong></td><td><strong style='color: #d9534f; font-size: 15px;'>{{overdue_minutes}} phút</strong></td></tr>
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
                    <td style='padding: 8px 10px;'>{{origin_gate}}</td>
                    <td style='padding: 8px 10px;'>{{checkout_origin_time}}</td>
                    <td style='padding: 8px 10px;'>--</td>
                    <td style='padding: 8px 10px;'><span style='color: green; font-weight: bold;'>Đã xuất phát</span></td>
                </tr>
                <tr style='border-bottom: 1px solid #eee; background-color: #ffeef0;'>
                    <td style='padding: 8px 10px;'>2. CHECK_IN Cổng đích</td>
                    <td style='padding: 8px 10px;'>{{destination_gate}}</td>
                    <td style='padding: 8px 10px;'><strong style='color: red;'>Chưa ghi nhận</strong></td>
                    <td style='padding: 8px 10px;'>{{deadline_checkin_dest}}</td>
                    <td style='padding: 8px 10px;'><strong style='color: #d9534f;'>QUÁ HẠN {{overdue_minutes}} PHÚT</strong></td>
                </tr>
                <tr style='border-bottom: 1px solid #eee;'>
                    <td style='padding: 8px 10px;'>3. CHECK_OUT Cổng đích</td>
                    <td style='padding: 8px 10px;'>{{destination_gate}}</td>
                    <td style='padding: 8px 10px;'>{{checkout_dest_time}}</td>
                    <td style='padding: 8px 10px;'>--</td>
                    <td style='padding: 8px 10px;'><span style='color: #888;'>Chưa đến</span></td>
                </tr>
                <tr>
                    <td style='padding: 8px 10px;'>4. CHECK_IN về Cổng xuất phát</td>
                    <td style='padding: 8px 10px;'>{{origin_gate}}</td>
                    <td style='padding: 8px 10px;'>{{checkin_origin_time}}</td>
                    <td style='padding: 8px 10px;'>--</td>
                    <td style='padding: 8px 10px;'><span style='color: #888;'>Chưa đến</span></td>
                </tr>
            </tbody>
        </table>

        <div style='margin-top: 14px; padding: 10px 14px; background-color: #fff3cd; border-left: 4px solid #ffc107; color: #856404; font-size: 13px;'>
            <strong>Khuyến nghị:</strong> Xe đã rời {{origin_gate}} nhưng chưa đến {{destination_gate}} quá thời gian quy định. Vui lòng kiểm tra phương tiện trên tuyến đường nội bộ hoặc camera khu vực cổng.
        </div>
        <hr style='border: none; border-top: 1px solid #eee; margin: 18px 0 12px 0;' />
        <p style='margin: 0; font-size: 12px; color: #888;'>Hệ thống HPParking Alert Service • Thời điểm cảnh báo: {{alert_time}}</p>
    </div>
</div>",
                    Recipients = defaultRecipients,
                    Enabled = true,
                    AvailableVariables = standardVariables
                };
                await UpsertTemplateAsync(templateRepo, t1, logger);

                // Mẫu 2: Xe chưa CHECK_OUT tại cổng đích (dừng bốc dỡ quá giờ)
                var t2 = new EmailTemplate
                {
                    TemplateCode = "TRIP_DESTINATION_TIMEOUT",
                    TemplateName = "Cảnh báo xe dừng bốc dỡ quá giờ tại cổng đích",
                    AlertType = "TRIP_DESTINATION_TIMEOUT",
                    SubjectTemplate = "⚠ [CẢNH BÁO XE QUÁ HẠN] Xe {{vehicle_code}} dừng quá giờ tại {{destination_gate}}",
                    BodyTemplate = @"
<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 700px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 6px; overflow: hidden;'>
    <div style='background-color: #f0ad4e; color: white; padding: 14px 20px;'>
        <h2 style='margin: 0; font-size: 18px;'>⚠️ CẢNH BÁO XE DỪNG QUÁ GIỜ TẠI CỔNG ĐÍCH</h2>
        <p style='margin: 4px 0 0 0; font-size: 12px; opacity: 0.9;'>Hệ thống Giám sát Hành trình Xe nâng UHF - HPParking</p>
    </div>
    <div style='padding: 18px; background-color: #ffffff;'>
        <p style='margin-top: 0;'>Kính gửi Bộ phận Nhân sự & Quản đốc Bốc dỡ,</p>
        <p>Hệ thống ghi nhận xe <strong>{{vehicle_code}}</strong> đã CHECK_IN tại <strong>{{destination_gate}}</strong> nhưng chưa CHECK_OUT rời cổng trong thời gian bốc dỡ cho phép.</p>
        
        <table style='width: 100%; border-collapse: collapse; margin-bottom: 12px;'>
            <tr><td style='padding: 4px 0; width: 180px;'><strong>Biển số xe:</strong></td><td><span style='color: #f0ad4e; font-weight: bold; font-size: 15px;'>{{vehicle_code}}</span></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Mã UHF / EPC:</strong></td><td><code>{{tag_code}}</code></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Cổng dừng đỗ / đích:</strong></td><td><strong>{{destination_gate}}</strong></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Thời gian bốc dỡ cho phép:</strong></td><td>{{allowed_minutes}} phút</td></tr>
            <tr><td style='padding: 4px 0;'><strong>Thời gian đã quá hạn:</strong></td><td><strong style='color: #f0ad4e; font-size: 15px;'>{{overdue_minutes}} phút</strong></td></tr>
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
                    <td style='padding: 8px 10px;'>{{origin_gate}}</td>
                    <td style='padding: 8px 10px;'>{{checkout_origin_time}}</td>
                    <td style='padding: 8px 10px;'>--</td>
                    <td style='padding: 8px 10px;'><span style='color: green; font-weight: bold;'>Đã xuất phát</span></td>
                </tr>
                <tr style='border-bottom: 1px solid #eee;'>
                    <td style='padding: 8px 10px;'>2. CHECK_IN Cổng đích</td>
                    <td style='padding: 8px 10px;'>{{destination_gate}}</td>
                    <td style='padding: 8px 10px;'>{{checkin_dest_time}}</td>
                    <td style='padding: 8px 10px;'>{{deadline_checkin_dest}}</td>
                    <td style='padding: 8px 10px;'><span style='color: green;'>Đã đến ({{travel_to_dest_minutes}}p)</span></td>
                </tr>
                <tr style='border-bottom: 1px solid #eee; background-color: #fff9e6;'>
                    <td style='padding: 8px 10px;'>3. CHECK_OUT Cổng đích</td>
                    <td style='padding: 8px 10px;'>{{destination_gate}}</td>
                    <td style='padding: 8px 10px;'><strong style='color: orange;'>Chưa rời cổng</strong></td>
                    <td style='padding: 8px 10px;'>{{deadline_checkout_dest}}</td>
                    <td style='padding: 8px 10px;'><strong style='color: #f0ad4e;'>QUÁ HẠN BỐC DỠ {{overdue_minutes}} PHÚT</strong></td>
                </tr>
                <tr>
                    <td style='padding: 8px 10px;'>4. CHECK_IN về Cổng xuất phát</td>
                    <td style='padding: 8px 10px;'>{{origin_gate}}</td>
                    <td style='padding: 8px 10px;'>{{checkin_origin_time}}</td>
                    <td style='padding: 8px 10px;'>--</td>
                    <td style='padding: 8px 10px;'><span style='color: #888;'>Chưa đến</span></td>
                </tr>
            </tbody>
        </table>

        <div style='margin-top: 14px; padding: 10px 14px; background-color: #fff3cd; border-left: 4px solid #ffc107; color: #856404; font-size: 13px;'>
            <strong>Khuyến nghị:</strong> Phương tiện đã dừng tại cổng {{destination_gate}} quá thời gian bốc dỡ định mức. Vui lòng kiểm tra tiến độ giao nhận hoặc lý do xe lưu bến kéo dài.
        </div>
        <hr style='border: none; border-top: 1px solid #eee; margin: 18px 0 12px 0;' />
        <p style='margin: 0; font-size: 12px; color: #888;'>Hệ thống HPParking Alert Service • Thời điểm cảnh báo: {{alert_time}}</p>
    </div>
</div>",
                    Recipients = defaultRecipients,
                    Enabled = true,
                    AvailableVariables = standardVariables
                };
                await UpsertTemplateAsync(templateRepo, t2, logger);

                // Mẫu 3: Xe chưa quay về cổng xuất phát (Quá hạn di chuyển từ Cổng phụ -> Cổng xuất phát)
                var t3 = new EmailTemplate
                {
                    TemplateCode = "TRIP_RETURN_TIMEOUT",
                    TemplateName = "Cảnh báo xe quá hạn quay về cổng xuất phát",
                    AlertType = "TRIP_RETURN_TIMEOUT",
                    SubjectTemplate = "⚠ [CẢNH BÁO XE QUÁ HẠN] Xe {{vehicle_code}} chưa quay về {{origin_gate}}",
                    BodyTemplate = @"
<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 700px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 6px; overflow: hidden;'>
    <div style='background-color: #d9534f; color: white; padding: 14px 20px;'>
        <h2 style='margin: 0; font-size: 18px;'>⚠️ CẢNH BÁO XE CHƯA QUAY VỀ CỔNG XUẤT PHÁT</h2>
        <p style='margin: 4px 0 0 0; font-size: 12px; opacity: 0.9;'>Hệ thống Giám sát Hành trình Xe nâng UHF - HPParking</p>
    </div>
    <div style='padding: 18px; background-color: #ffffff;'>
        <p style='margin-top: 0;'>Kính gửi Bộ phận Nhân sự & Quản đốc Điều phối,</p>
        <p>Hệ thống ghi nhận xe <strong>{{vehicle_code}}</strong> đã CHECK_OUT rời <strong>{{destination_gate}}</strong> nhưng chưa CHECK_IN về lại <strong>{{origin_gate}}</strong> trong thời gian cho phép.</p>
        
        <table style='width: 100%; border-collapse: collapse; margin-bottom: 12px;'>
            <tr><td style='padding: 4px 0; width: 180px;'><strong>Biển số xe:</strong></td><td><span style='color: #d9534f; font-weight: bold; font-size: 15px;'>{{vehicle_code}}</span></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Mã UHF / EPC:</strong></td><td><code>{{tag_code}}</code></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Cổng xuất phát cần về:</strong></td><td><strong>{{origin_gate}}</strong></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Thời gian cho phép quay về:</strong></td><td>{{allowed_minutes}} phút</td></tr>
            <tr><td style='padding: 4px 0;'><strong>Thời gian đã quá hạn:</strong></td><td><strong style='color: #d9534f; font-size: 15px;'>{{overdue_minutes}} phút</strong></td></tr>
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
                    <td style='padding: 8px 10px;'>{{origin_gate}}</td>
                    <td style='padding: 8px 10px;'>{{checkout_origin_time}}</td>
                    <td style='padding: 8px 10px;'>--</td>
                    <td style='padding: 8px 10px;'><span style='color: green; font-weight: bold;'>Đã xuất phát</span></td>
                </tr>
                <tr style='border-bottom: 1px solid #eee;'>
                    <td style='padding: 8px 10px;'>2. CHECK_IN Cổng đích</td>
                    <td style='padding: 8px 10px;'>{{destination_gate}}</td>
                    <td style='padding: 8px 10px;'>{{checkin_dest_time}}</td>
                    <td style='padding: 8px 10px;'>{{deadline_checkin_dest}}</td>
                    <td style='padding: 8px 10px;'><span style='color: green;'>Đã đến ({{travel_to_dest_minutes}}p)</span></td>
                </tr>
                <tr style='border-bottom: 1px solid #eee;'>
                    <td style='padding: 8px 10px;'>3. CHECK_OUT Cổng đích</td>
                    <td style='padding: 8px 10px;'>{{destination_gate}}</td>
                    <td style='padding: 8px 10px;'>{{checkout_dest_time}}</td>
                    <td style='padding: 8px 10px;'>{{deadline_checkout_dest}}</td>
                    <td style='padding: 8px 10px;'><span style='color: green;'>Đã rời cổng ({{stay_at_dest_minutes}}p)</span></td>
                </tr>
                <tr style='background-color: #ffeef0;'>
                    <td style='padding: 8px 10px;'>4. CHECK_IN về Cổng xuất phát</td>
                    <td style='padding: 8px 10px;'>{{origin_gate}}</td>
                    <td style='padding: 8px 10px;'><strong style='color: red;'>Chưa quay về</strong></td>
                    <td style='padding: 8px 10px;'>{{deadline_checkin_origin}}</td>
                    <td style='padding: 8px 10px;'><strong style='color: #d9534f;'>QUÁ HẠN VỀ {{overdue_minutes}} PHÚT</strong></td>
                </tr>
            </tbody>
        </table>

        <div style='margin-top: 14px; padding: 10px 14px; background-color: #fff3cd; border-left: 4px solid #ffc107; color: #856404; font-size: 13px;'>
            <strong>Khuyến nghị:</strong> Xe đã rời {{destination_gate}} nhưng chưa thấy CHECK_IN về lại {{origin_gate}} đúng hạn. Vui lòng kiểm tra tuyến đường nội bộ hoặc gọi tài xế.
        </div>
        <hr style='border: none; border-top: 1px solid #eee; margin: 18px 0 12px 0;' />
        <p style='margin: 0; font-size: 12px; color: #888;'>Hệ thống HPParking Alert Service • Thời điểm cảnh báo: {{alert_time}}</p>
    </div>
</div>",
                    Recipients = defaultRecipients,
                    Enabled = true,
                    AvailableVariables = standardVariables
                };
                await UpsertTemplateAsync(templateRepo, t3, logger);

                // Mẫu 4: Phát hiện chuyến đi bất thường (Thiếu CHECK_OUT, trốn việc, nhảy cóc mốc)
                var t4 = new EmailTemplate
                {
                    TemplateCode = "TRIP_ABNORMAL",
                    TemplateName = "Cảnh báo hành trình bất thường (Thiếu Check-out / Trốn việc)",
                    AlertType = "TRIP_ABNORMAL",
                    SubjectTemplate = "⚠ [CẢNH BÁO BẤT THƯỜNG] Xe {{vehicle_code}} về {{origin_gate}} KHÔNG CÓ CHECK-OUT tại {{destination_gate}}",
                    BodyTemplate = @"
<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 700px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 6px; overflow: hidden;'>
    <div style='background-color: #d9534f; color: white; padding: 14px 20px;'>
        <h2 style='margin: 0; font-size: 18px;'>⚠️ PHÁT HIỆN HÀNH TRÌNH BẤT THƯỜNG - SAI QUY TRÌNH KIỂM SOÁT</h2>
        <p style='margin: 4px 0 0 0; font-size: 12px; opacity: 0.9;'>Hệ thống Giám sát Hành trình Xe nâng UHF - HPParking</p>
    </div>
    <div style='padding: 18px; background-color: #ffffff;'>
        <p style='margin-top: 0;'>Kính gửi Bộ phận Nhân sự & Quản đốc Điều phối,</p>
        <p>Hệ thống ghi nhận xe <strong>{{vehicle_code}}</strong> có dấu hiệu <strong>không tuân thủ quy trình bốc dỡ hàng hoặc rời cổng sai quy định</strong>:</p>
        
        <table style='width: 100%; border-collapse: collapse; margin-bottom: 12px;'>
            <tr><td style='padding: 4px 0; width: 200px;'><strong>Biển số xe:</strong></td><td><span style='color: #d9534f; font-weight: bold; font-size: 15px;'>{{vehicle_code}}</span></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Mã UHF / EPC:</strong></td><td><code>{{tag_code}}</code></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Cổng xuất phát (Cổng gán):</strong></td><td><strong>{{origin_gate}}</strong></td></tr>
            <tr><td style='padding: 4px 0;'><strong>Cổng phụ bốc dỡ:</strong></td><td><strong>{{destination_gate}}</strong></td></tr>
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
                    <td style='padding: 8px 10px;'>{{origin_gate}}</td>
                    <td style='padding: 8px 10px;'>{{checkout_origin_time}}</td>
                    <td style='padding: 8px 10px;'><span style='color: green; font-weight: bold;'>Đã xuất phát</span></td>
                </tr>
                <tr style='border-bottom: 1px solid #eee;'>
                    <td style='padding: 8px 10px;'>2. CHECK_IN Cổng phụ</td>
                    <td style='padding: 8px 10px;'>{{destination_gate}}</td>
                    <td style='padding: 8px 10px;'>{{checkin_dest_time}}</td>
                    <td style='padding: 8px 10px;'><span style='color: green; font-weight: bold;'>Đã đến ({{travel_to_dest_minutes}}p)</span></td>
                </tr>
                <tr style='border-bottom: 1px solid #eee; background-color: #ffeef0;'>
                    <td style='padding: 8px 10px; font-weight: bold; color: red;'>3. CHECK_OUT Cổng phụ</td>
                    <td style='padding: 8px 10px;'>{{destination_gate}}</td>
                    <td style='padding: 8px 10px;'><strong style='color: red;'>{{checkout_dest_time}}</strong></td>
                    <td style='padding: 8px 10px;'><span style='color: red; font-weight: bold;'>THIẾU / BỎ QUA CHECK-OUT</span></td>
                </tr>
                <tr>
                    <td style='padding: 8px 10px;'>4. CHECK_IN về Cổng xuất phát</td>
                    <td style='padding: 8px 10px;'>{{origin_gate}}</td>
                    <td style='padding: 8px 10px;'><strong>{{checkin_origin_time}}</strong></td>
                    <td style='padding: 8px 10px;'><span style='color: orange; font-weight: bold;'>Đột ngột xuất hiện</span></td>
                </tr>
            </tbody>
        </table>

        <div style='margin-top: 14px; padding: 10px 14px; background-color: #fff3cd; border-left: 4px solid #ffc107; color: #856404; font-size: 13px;'>
            <strong>Lý do cảnh báo:</strong> {{reason}}
            <br/><strong>Tổng thời gian từ khi đi đến khi về:</strong> {{total_trip_minutes}} phút.
            <br/><em>Kính đề nghị Quản đốc / Nhân sự trích xuất Camera tại cổng {{destination_gate}} và đối chiếu Phiếu bốc dỡ hàng thực tế.</em>
        </div>
        <hr style='border: none; border-top: 1px solid #eee; margin: 18px 0 12px 0;' />
        <p style='margin: 0; font-size: 12px; color: #888;'>Hệ thống HPParking Alert Service • Thời điểm phát hiện: {{alert_time}}</p>
    </div>
</div>",
                    Recipients = defaultRecipients,
                    Enabled = true,
                    AvailableVariables = standardVariables
                };
                await UpsertTemplateAsync(templateRepo, t4, logger);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[DbSeeder] Không thể khởi tạo mẫu email cảnh báo ban đầu: {Message}", ex.Message);
            }
        }

        private static async Task UpsertTemplateAsync(
            IRepository<EmailTemplate> templateRepo, 
            EmailTemplate newTemplate, 
            ILogger? logger)
        {
            var existing = await templateRepo.FindOneAsync(t => t.TemplateCode == newTemplate.TemplateCode);
            if (existing == null)
            {
                await templateRepo.AddAsync(newTemplate);
                logger?.LogInformation("[DbSeeder] Khởi tạo mẫu email cảnh báo {Code} thành công.", newTemplate.TemplateCode);
            }
            else
            {
                existing.TemplateName = newTemplate.TemplateName;
                existing.AlertType = newTemplate.AlertType;
                existing.SubjectTemplate = newTemplate.SubjectTemplate;
                existing.BodyTemplate = newTemplate.BodyTemplate;
                existing.AvailableVariables = newTemplate.AvailableVariables;
                if (existing.Recipients == null || existing.Recipients.Count == 0)
                {
                    existing.Recipients = newTemplate.Recipients;
                }
                await templateRepo.UpdateAsync(existing);
                logger?.LogInformation("[DbSeeder] Đồng bộ cập nhật mẫu email cảnh báo {Code} lên phiên bản mới nhất.", newTemplate.TemplateCode);
            }
        }
    }
}
