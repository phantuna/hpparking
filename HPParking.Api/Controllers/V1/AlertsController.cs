using Asp.Versioning;
using HPParking.Api.DTOs.Alerts;
using HPParking.Api.DTOs.Common;
using HPParking.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HPParking.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/alerts")]
    public class AlertsController : BaseApiController
    {
        private readonly IEmailAlertService _emailAlertService;

        public AlertsController(
            IEmailAlertService emailAlertService,
            ILogger<AlertsController> logger) : base(logger)
        {
            _emailAlertService = emailAlertService;
        }

        /// <summary>
        /// Test nhanh gửi email cảnh báo với dữ liệu mẫu (Không cần nhập body JSON)
        /// </summary>
        [HttpGet("test-quick")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        public IActionResult TestQuick()
        {
            var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "bg-office.jpg");
            if (!System.IO.File.Exists(imagePath))
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                if (Directory.Exists(uploadsDir))
                {
                    imagePath = Directory.GetFiles(uploadsDir, "*.*")
                        .FirstOrDefault(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) ?? "";
                }
            }

            var sampleTrip = new InterGateTripAlertModel
            {
                PlateNumber = "29A-888.99",
                ClientName = "Nguyễn Văn Test",
                DepartmentName = "Phòng Vận Tải & Điều Phối",
                OriginGateName = "Cổng Chính (Kho A)",
                DestinationGateName = "Cổng Phụ (Kho B)",
                CheckOutOriginTime = DateTime.Now.AddMinutes(-35),
                CheckInDestTime = DateTime.Now,
                ActualTravelMinutes = 35,
                MaxAllowedMinutes = 15,
                AlertReason = "Di chuyển vượt định mức cho phép (+20 phút)",
                OverviewImagePath = imagePath
            };

            _emailAlertService.EnqueueInterGateAlert(sampleTrip);
            return OkApiResponse($"Đã kích hoạt gửi thử email cảnh báo mẫu kèm ảnh demo ({Path.GetFileName(imagePath)}) vào hàng đợi nền. Vui lòng kiểm tra hòm thư của bạn!", "Thành công.");
        }

        /// <summary>
        /// Kích hoạt gửi email cảnh báo xe di chuyển liên cổng quá giờ (Kèm ảnh CID)
        /// </summary>
        [HttpPost("inter-gate-trip")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        public IActionResult SendInterGateAlert([FromBody] InterGateTripAlertModel request)
        {
            _emailAlertService.EnqueueInterGateAlert(request);
            return OkApiResponse("Đã tiếp nhận yêu cầu gửi cảnh báo di chuyển liên cổng vào hàng đợi nền.", "Thành công.");
        }

        /// <summary>
        /// Kích hoạt gửi email cảnh báo nhân viên vào cổng muộn (Kèm ảnh CID)
        /// </summary>
        [HttpPost("late-checkin")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        public IActionResult SendLateCheckInAlert([FromBody] LateCheckInAlertModel request)
        {
            _emailAlertService.EnqueueLateCheckInAlert(request);
            return OkApiResponse("Đã tiếp nhận yêu cầu gửi cảnh báo vào cổng muộn vào hàng đợi nền.", "Thành công.");
        }
    }
}
