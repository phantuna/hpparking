using HPParking.Core.Models.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace HPParking.Api.Services.Interfaces
{
    /// <summary>
    /// Dịch vụ gửi email thông báo và cảnh báo vi phạm thời gian xe nâng di chuyển liên cổng
    /// </summary>
    public interface IEmailAlertService
    {
        /// <summary>
        /// Gửi email cảnh báo Mốc 1: Xe CHECK_OUT cổng xuất phát nhưng quá giờ chưa CHECK_IN cổng đích
        /// </summary>
        Task SendMissingCheckInDestAlertAsync(VehicleTrip trip, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gửi email cảnh báo Mốc 2: Xe CHECK_IN tại cổng đích nhưng dừng quá thời gian cho phép mà chưa CHECK_OUT
        /// </summary>
        Task SendOverstayDestAlertAsync(VehicleTrip trip, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gửi email cảnh báo Mốc 3: Xe CHECK_OUT rời cổng đích nhưng quá giờ chưa CHECK_IN về cổng xuất phát
        /// </summary>
        Task SendMissingCheckInOriginAlertAsync(VehicleTrip trip, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gửi email cảnh báo Bất thường: Xe về lại cổng gán nhưng KHÔNG HỀ CÓ CHECK_OUT tại cổng phụ (nghi vấn trốn việc / né kiểm soát)
        /// </summary>
        Task SendAbnormalTripAlertAsync(VehicleTrip trip, string reason, CancellationToken cancellationToken = default);
    }
}
