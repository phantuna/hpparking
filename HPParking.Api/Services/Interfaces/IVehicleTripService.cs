using HPParking.Api.DTOs.Common;
using HPParking.Api.DTOs.VehicleTrips;
using HPParking.Core.Models.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HPParking.Api.Services.Interfaces
{
    /// <summary>
    /// Dịch vụ quản lý State Machine và vòng đời hành trình di chuyển liên cổng
    /// </summary>
    public interface IVehicleTripService
    {
        /// <summary>
        /// Xử lý một sự kiện cổng vừa được ghi nhận từ đầu đọc UHF để cập nhật State Machine của Trip
        /// </summary>
        Task<VehicleTrip?> ProcessGateEventAsync(VehicleGateEvent gateEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách các chuyến xe đang hoạt động (chưa hoàn thành: InTransitToDest, AtDestination, Returning)
        /// </summary>
        Task<IReadOnlyList<VehicleTrip>> GetActiveTripsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách chuyến xe có phân trang và bộ lọc
        /// </summary>
        Task<PagedResult<VehicleTripDto>> GetTripsPagedAsync(int pageIndex, int pageSize, string? vehicleId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cập nhật cờ cảnh báo đã gửi cho Trip
        /// </summary>
        Task MarkAlertSentAsync(string tripId, int alertMilestone, CancellationToken cancellationToken = default);
    }
}
