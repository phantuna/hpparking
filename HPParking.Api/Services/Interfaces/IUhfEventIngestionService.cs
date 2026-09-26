using HPParking.Api.DTOs.VehicleTrips;
using HPParking.Core.Models.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace HPParking.Api.Services.Interfaces
{
    /// <summary>
    /// Dịch vụ tiếp nhận sự kiện UHF từ phần cứng, lọc chống nhiễu quét lặp (Debounce) và ghi nhận vào hệ thống
    /// </summary>
    public interface IUhfEventIngestionService
    {
        /// <summary>
        /// Tiếp nhận và xử lý sự kiện quét thẻ UHF
        /// Trả về bản ghi VehicleGateEvent đã lưu hoặc null nếu bị lọc trùng (Debounce) / không hợp lệ
        /// </summary>
        Task<VehicleGateEvent?> IngestRawEventAsync(UhfRawEventDto rawEvent, CancellationToken cancellationToken = default);
    }
}
