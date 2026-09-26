using HPParking.Api.Services.Interfaces;
using HPParking.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HPParking.Api.BackgroundServices
{
    /// <summary>
    /// Background Service chạy ngầm định kỳ quét các hành trình xe nâng liên cổng,
    /// phát hiện vi phạm các mốc deadline thời gian và kích hoạt gửi email cảnh báo cho HR/Quản đốc
    /// Tích hợp cơ chế IServiceScope quản lý vòng đời ngắn để tránh rò rỉ bộ nhớ (Memory Leak)
    /// </summary>
    public class VehicleTripTimeoutWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<VehicleTripTimeoutWorker> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

        public VehicleTripTimeoutWorker(
            IServiceProvider serviceProvider,
            ILogger<VehicleTripTimeoutWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[VehicleTripTimeoutWorker] Dịch vụ quét timeout hành trình xe nâng UHF đã khởi động. Chu kỳ: {Interval}s", Interval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckTripsTimeoutAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Dịch vụ đang tắt an toàn, không xử lý lỗi
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[VehicleTripTimeoutWorker] Đã xảy ra lỗi trong chu kỳ quét timeout xe nâng");
                }

                await Task.Delay(Interval, stoppingToken);
            }

            _logger.LogInformation("[VehicleTripTimeoutWorker] Dịch vụ quét timeout hành trình xe nâng đã dừng lại an toàn.");
        }

        private async Task CheckTripsTimeoutAsync(CancellationToken cancellationToken)
        {
            // TẠO SCOPE NGẮN (Scoped Lifetime) cho mỗi chu kỳ quét:
            // Đảm bảo toàn bộ DbContext, Repositories, và Service instance được giải phóng ngay sau mỗi lần quét,
            // ngăn chặn việc giữ tham chiếu trong Singleton service gây rò rỉ bộ nhớ (Memory Leak).
            using var scope = _serviceProvider.CreateScope();
            var tripService = scope.ServiceProvider.GetRequiredService<IVehicleTripService>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailAlertService>();

            var activeTrips = await tripService.GetActiveTripsAsync(cancellationToken);
            if (activeTrips.Count == 0)
            {
                return;
            }

            var now = DateTime.Now;

            foreach (var trip in activeTrips)
            {
                // KIỂM TRA MỐC 1: Quá hạn di chuyển sang Cổng đích (Chưa CHECK_IN tại Cổng đích)
                if (trip.Status == VehicleTripStatus.InTransitToDest &&
                    now > trip.DeadlineCheckInDest &&
                    !trip.AlertMissingCheckInDestSent)
                {
                    _logger.LogWarning("[VehicleTripTimeoutWorker] [VI PHẠM MỐC 1] Xe {Plate} quá hạn chưa CHECK_IN tại {Dest}! Deadline: {Deadline:HH:mm:ss}",
                        trip.PlateNumber, trip.DestinationGateCode, trip.DeadlineCheckInDest);

                    await emailService.SendMissingCheckInDestAlertAsync(trip, cancellationToken);
                    await tripService.MarkAlertSentAsync(trip.Id, 1, cancellationToken);
                }

                // KIỂM TRA MỐC 2: Quá thời gian bốc dỡ hàng tại Cổng đích (Chưa CHECK_OUT khỏi Cổng đích)
                if (trip.Status == VehicleTripStatus.AtDestination &&
                    trip.DeadlineCheckOutDest.HasValue &&
                    now > trip.DeadlineCheckOutDest.Value &&
                    !trip.AlertOverstayDestSent)
                {
                    _logger.LogWarning("[VehicleTripTimeoutWorker] [VI PHẠM MỐC 2] Xe {Plate} dừng quá giờ tại Cổng đích {Dest}! Deadline: {Deadline:HH:mm:ss}",
                        trip.PlateNumber, trip.DestinationGateCode, trip.DeadlineCheckOutDest.Value);

                    await emailService.SendOverstayDestAlertAsync(trip, cancellationToken);
                    await tripService.MarkAlertSentAsync(trip.Id, 2, cancellationToken);
                }

                // KIỂM TRA MỐC 3: Quá hạn quay về Cổng xuất phát (Chưa CHECK_IN về Cổng xuất phát)
                if (trip.Status == VehicleTripStatus.Returning &&
                    trip.DeadlineCheckInOrigin.HasValue &&
                    now > trip.DeadlineCheckInOrigin.Value &&
                    !trip.AlertMissingCheckInOriginSent)
                {
                    _logger.LogWarning("[VehicleTripTimeoutWorker] [VI PHẠM MỐC 3] Xe {Plate} quá hạn chưa quay về Cổng xuất phát {Origin}! Deadline: {Deadline:HH:mm:ss}",
                        trip.PlateNumber, trip.OriginGateCode, trip.DeadlineCheckInOrigin.Value);

                    await emailService.SendMissingCheckInOriginAlertAsync(trip, cancellationToken);
                    await tripService.MarkAlertSentAsync(trip.Id, 3, cancellationToken);
                }
            }
        }
    }
}
