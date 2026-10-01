using HPParking.Api.DTOs.Alerts;

namespace HPParking.Api.Common.Email
{
    public static class EmailTemplateBuilder
    {
        public static string BuildInterGateTripAlertHtml(InterGateTripAlertModel model, string cidImage)
        {
            var clientName = !string.IsNullOrWhiteSpace(model.ClientName) ? model.ClientName : "Chưa xác định";
            var deptName = !string.IsNullOrWhiteSpace(model.DepartmentName) ? model.DepartmentName : "N/A";

            return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>Cảnh Báo Di Chuyển Liên Cổng Quá Giờ</title>
  <style>
    body {{ margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; }}
    .container {{ max-width: 600px; margin: 30px auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.08); border: 1px solid #e2e8f0; }}
    .header {{ background: linear-gradient(135deg, #b91c1c 0%, #dc2626 100%); color: #ffffff; padding: 28px 24px; text-align: center; }}
    .header h1 {{ margin: 0; font-size: 22px; font-weight: 700; letter-spacing: -0.5px; }}
    .header p {{ margin: 6px 0 0; opacity: 0.9; font-size: 14px; }}
    .content {{ padding: 24px; }}
    .badge {{ display: inline-block; padding: 6px 12px; background: #fee2e2; color: #991b1b; font-size: 13px; font-weight: 600; border-radius: 20px; margin-bottom: 16px; border: 1px solid #fecaca; }}
    .info-card {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; margin-bottom: 20px; }}
    .info-row {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #edf2f7; font-size: 14px; }}
    .info-row:last-child {{ border-bottom: none; }}
    .info-label {{ color: #64748b; font-weight: 500; }}
    .info-val {{ color: #0f172a; font-weight: 600; text-align: right; }}
    .highlight-red {{ color: #dc2626; font-weight: 700; }}
    .image-box {{ margin-top: 20px; border-radius: 8px; overflow: hidden; border: 1px solid #cbd5e1; background: #000; text-align: center; }}
    .image-box img {{ width: 100%; max-height: 380px; object-fit: contain; display: block; }}
    .image-caption {{ padding: 8px 12px; font-size: 12px; color: #64748b; background: #f8fafc; border-top: 1px solid #e2e8f0; text-align: center; font-style: italic; }}
    .footer {{ background: #f8fafc; padding: 16px 24px; text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>⚠️ CẢNH BÁO VI PHẠM LIÊN CỔNG</h1>
      <p>Hệ thống giám sát an ninh tự động HPParking</p>
    </div>
    <div class=""content"">
      <div style=""text-align: center;"">
        <span class=""badge"">{model.AlertReason}</span>
      </div>

      <div class=""info-card"">
        <div class=""info-row"">
          <span class=""info-label"">🚗 Biển số phương tiện:</span>
          <span class=""info-val"" style=""font-size: 16px; color: #1e3a8a;"">{model.PlateNumber}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">👤 Người điều khiển / Đơn vị:</span>
          <span class=""info-val"">{clientName} ({deptName})</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">🚪 Tuyến cổng:</span>
          <span class=""info-val"">{model.OriginGateName} ➔ {model.DestinationGateName}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">⏱️ Giờ xuất phát (Cổng đi):</span>
          <span class=""info-val"">{model.CheckOutOriginTime:dd/MM/yyyy HH:mm:ss}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">⏱️ Giờ đến (Cổng đích):</span>
          <span class=""info-val"">{model.CheckInDestTime:dd/MM/yyyy HH:mm:ss}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">⏳ Định mức quy định:</span>
          <span class=""info-val"">{model.MaxAllowedMinutes} phút</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">⌛ Thời gian thực tế:</span>
          <span class=""info-val highlight-red"">{model.ActualTravelMinutes:F1} phút</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">🚨 Số phút vượt quá:</span>
          <span class=""info-val highlight-red"">+{model.OverdueMinutes:F1} phút</span>
        </div>
      </div>

      <div style=""font-weight: 600; font-size: 14px; margin-bottom: 8px; color: #334155;"">
        📸 Ảnh bằng chứng từ Camera Toàn Cảnh:
      </div>
      <div class=""image-box"">
        <img src=""cid:{cidImage}"" alt=""Ảnh toàn cảnh vi phạm"" />
        <div class=""image-caption"">Ảnh chụp tự động tại thời điểm ghi nhận sự kiện ({model.CheckInDestTime:dd/MM/yyyy HH:mm:ss})</div>
      </div>
    </div>
    <div class=""footer"">
      Email này được tạo tự động bởi hệ thống Quản lý Bãi xe HPParking.<br/>
      Vui lòng không trả lời trực tiếp email này.
    </div>
  </div>
</body>
</html>";
        }

        public static string BuildLateCheckInAlertHtml(LateCheckInAlertModel model, string cidImage)
        {
            var empCode = !string.IsNullOrWhiteSpace(model.EmployeeCode) ? model.EmployeeCode : "N/A";
            var deptName = !string.IsNullOrWhiteSpace(model.DepartmentName) ? model.DepartmentName : "N/A";
            var plate = !string.IsNullOrWhiteSpace(model.PlateNumber) ? model.PlateNumber : "Không sử dụng xe";

            return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>Cảnh Báo Nhân Viên Vào Cổng Muộn</title>
  <style>
    body {{ margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; }}
    .container {{ max-width: 600px; margin: 30px auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.08); border: 1px solid #e2e8f0; }}
    .header {{ background: linear-gradient(135deg, #d97706 0%, #f59e0b 100%); color: #ffffff; padding: 28px 24px; text-align: center; }}
    .header h1 {{ margin: 0; font-size: 22px; font-weight: 700; letter-spacing: -0.5px; }}
    .header p {{ margin: 6px 0 0; opacity: 0.9; font-size: 14px; }}
    .content {{ padding: 24px; }}
    .badge {{ display: inline-block; padding: 6px 12px; background: #fef3c7; color: #b45309; font-size: 13px; font-weight: 600; border-radius: 20px; margin-bottom: 16px; border: 1px solid #fde68a; }}
    .info-card {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; margin-bottom: 20px; }}
    .info-row {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #edf2f7; font-size: 14px; }}
    .info-row:last-child {{ border-bottom: none; }}
    .info-label {{ color: #64748b; font-weight: 500; }}
    .info-val {{ color: #0f172a; font-weight: 600; text-align: right; }}
    .highlight-amber {{ color: #d97706; font-weight: 700; }}
    .image-box {{ margin-top: 20px; border-radius: 8px; overflow: hidden; border: 1px solid #cbd5e1; background: #000; text-align: center; }}
    .image-box img {{ width: 100%; max-height: 380px; object-fit: contain; display: block; }}
    .image-caption {{ padding: 8px 12px; font-size: 12px; color: #64748b; background: #f8fafc; border-top: 1px solid #e2e8f0; text-align: center; font-style: italic; }}
    .footer {{ background: #f8fafc; padding: 16px 24px; text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>⏰ THÔNG BÁO VÀO CỔNG MUỘN</h1>
      <p>Hệ thống giám sát chấm công & an ninh HPParking</p>
    </div>
    <div class=""content"">
      <div style=""text-align: center;"">
        <span class=""badge"">Vượt quá thời gian bắt đầu ca quy định</span>
      </div>

      <div class=""info-card"">
        <div class=""info-row"">
          <span class=""info-label"">👤 Họ và tên nhân viên:</span>
          <span class=""info-val"" style=""font-size: 16px; color: #1e3a8a;"">{model.EmployeeName}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">🆔 Mã / SĐT:</span>
          <span class=""info-val"">{empCode}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">🏢 Phòng ban:</span>
          <span class=""info-val"">{deptName}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">🚗 Biển số xe:</span>
          <span class=""info-val"">{plate}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">🚪 Cổng vào:</span>
          <span class=""info-val"">{model.GateName}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">⏰ Giờ bắt đầu ca quy định:</span>
          <span class=""info-val"">{model.ShiftStartTime}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">⏱️ Giờ vào thực tế:</span>
          <span class=""info-val highlight-amber"">{model.CheckInTime:dd/MM/yyyy HH:mm:ss}</span>
        </div>
        <div class=""info-row"">
          <span class=""info-label"">🚨 Số phút đi muộn:</span>
          <span class=""info-val highlight-amber"">+{model.LateMinutes} phút</span>
        </div>
      </div>

      <div style=""font-weight: 600; font-size: 14px; margin-bottom: 8px; color: #334155;"">
        📸 Ảnh bằng chứng lúc vào cổng:
      </div>
      <div class=""image-box"">
        <img src=""cid:{cidImage}"" alt=""Ảnh toàn cảnh vào cổng muộn"" />
        <div class=""image-caption"">Ảnh chụp tự động tại cổng chính ({model.CheckInTime:dd/MM/yyyy HH:mm:ss})</div>
      </div>
    </div>
    <div class=""footer"">
      Email này được tạo tự động bởi hệ thống Quản lý Bãi xe HPParking.<br/>
      Vui lòng không trả lời trực tiếp email này.
    </div>
  </div>
</body>
</html>";
        }
    }
}
