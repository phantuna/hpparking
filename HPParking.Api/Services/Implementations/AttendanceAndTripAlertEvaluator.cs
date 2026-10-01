using HPParking.Api.Configuration;
using HPParking.Api.DTOs.Alerts;
using HPParking.Api.Services.Interfaces;
using HPParking.Core.Interfaces;
using HPParking.Core.Models.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HPParking.Api.Services.Implementations
{
    public interface IAttendanceAndTripAlertEvaluator
    {
        Task EvaluateLateCheckInAsync(Client client, string gateName, ParkingSession session, string? overviewImagePath);
        Task EvaluateInterGateTripAsync(string plateNumber, string originGateName, string destinationGateName, DateTime checkOutOriginTime, DateTime checkInDestTime, string? overviewImagePath, Client? client = null);
    }

    public class AttendanceAndTripAlertEvaluator : IAttendanceAndTripAlertEvaluator
    {
        private readonly IEmailAlertService _emailAlertService;
        private readonly WorkShiftSettings _shiftSettings;
        private readonly GateTripSettings _tripSettings;
        private readonly IRepository<Department> _departmentRepo;
        private readonly ILogger<AttendanceAndTripAlertEvaluator> _logger;

        public AttendanceAndTripAlertEvaluator(
            IEmailAlertService emailAlertService,
            IOptions<WorkShiftSettings> shiftOptions,
            IOptions<GateTripSettings> tripOptions,
            IRepository<Department> departmentRepo,
            ILogger<AttendanceAndTripAlertEvaluator> logger)
        {
            _emailAlertService = emailAlertService;
            _shiftSettings = shiftOptions.Value;
            _tripSettings = tripOptions.Value;
            _departmentRepo = departmentRepo;
            _logger = logger;
        }

        public async Task EvaluateLateCheckInAsync(Client client, string gateName, ParkingSession session, string? overviewImagePath)
        {
            if (!session.InTime.HasValue) return;

            var inTime = session.InTime.Value;
            if (TimeSpan.TryParse(_shiftSettings.ShiftStartTime, out var shiftStart))
            {
                var checkInTimeOfDay = inTime.TimeOfDay;
                var graceTime = shiftStart.Add(TimeSpan.FromMinutes(_shiftSettings.GracePeriodMinutes));

                // Nếu vào ca sáng sau giờ cho phép trễ (ví dụ quy định 08:00 + 15p = sau 08:15)
                if (checkInTimeOfDay > graceTime && checkInTimeOfDay < TimeSpan.FromHours(13))
                {
                    var lateMinutes = (int)Math.Round((checkInTimeOfDay - shiftStart).TotalMinutes);

                    string deptName = "Không xác định";
                    if (!string.IsNullOrWhiteSpace(client.DepartmentId))
                    {
                        var dept = await _departmentRepo.GetByIdAsync(client.DepartmentId);
                        if (dept != null) deptName = dept.Name;
                    }

                    var alertModel = new LateCheckInAlertModel
                    {
                        EmployeeName = client.Name,
                        EmployeeCode = client.Code ?? client.PhoneNumber,
                        DepartmentName = deptName,
                        PlateNumber = session.PlateNumber,
                        GateName = gateName,
                        CheckInTime = inTime,
                        ShiftStartTime = _shiftSettings.ShiftStartTime,
                        LateMinutes = lateMinutes,
                        OverviewImagePath = overviewImagePath
                    };

                    _logger.LogWarning("[AlertEvaluator] Phát hiện nhân viên {Name} vào cổng muộn {Minutes}p. Đang đưa vào hàng đợi gửi mail.", client.Name, lateMinutes);
                    _emailAlertService.EnqueueLateCheckInAlert(alertModel);
                }
            }
        }

        public Task EvaluateInterGateTripAsync(
            string plateNumber,
            string originGateName,
            string destinationGateName,
            DateTime checkOutOriginTime,
            DateTime checkInDestTime,
            string? overviewImagePath,
            Client? client = null)
        {
            var actualTravelMinutes = (checkInDestTime - checkOutOriginTime).TotalMinutes;
            if (actualTravelMinutes > _tripSettings.MaxTravelMinutes)
            {
                var alertModel = new InterGateTripAlertModel
                {
                    PlateNumber = plateNumber,
                    ClientName = client?.Name,
                    OriginGateName = originGateName,
                    DestinationGateName = destinationGateName,
                    CheckOutOriginTime = checkOutOriginTime,
                    CheckInDestTime = checkInDestTime,
                    ActualTravelMinutes = actualTravelMinutes,
                    MaxAllowedMinutes = _tripSettings.MaxTravelMinutes,
                    AlertReason = $"Di chuyển vượt định mức cho phép ({actualTravelMinutes:F1}p / {_tripSettings.MaxTravelMinutes}p)",
                    OverviewImagePath = overviewImagePath
                };

                _logger.LogWarning("[AlertEvaluator] Phát hiện xe {Plate} di chuyển liên cổng quá giờ ({Actual:F1}p / {Max}p). Đang gửi email cảnh báo.", plateNumber, actualTravelMinutes, _tripSettings.MaxTravelMinutes);
                _emailAlertService.EnqueueInterGateAlert(alertModel);
            }

            return Task.CompletedTask;
        }
    }
}
