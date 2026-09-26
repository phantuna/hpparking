using System;

namespace HPParking.Api.DTOs.VehicleTrips
{
    /// <summary>
    /// DTO nhận dữ liệu sự kiện quét thẻ từ Gateway / Thiết bị UHF tại cổng
    /// </summary>
    public class UhfRawEventDto
    {
        /// <summary>
        /// Mã định danh thiết bị đầu đọc UHF (ví dụ: UHF-C1-OUT, UHF-C2-IN)
        /// </summary>
        public string ReaderCode { get; set; } = string.Empty;

        /// <summary>
        /// ID thiết bị đầu đọc trong cơ sở dữ liệu (tùy chọn)
        /// </summary>
        public string? ReaderId { get; set; }

        /// <summary>
        /// Mã EPC của thẻ RFID/UHF gắn trên xe
        /// </summary>
        public string TagEpc { get; set; } = string.Empty;

        /// <summary>
        /// Mã xe hoặc biển số xe (tùy chọn nếu thiết bị/camera có nhận diện)
        /// </summary>
        public string? VehicleCode { get; set; }

        /// <summary>
        /// Chiều sự kiện (CHECK_IN hoặc CHECK_OUT) nếu thiết bị tự phân định (tùy chọn)
        /// </summary>
        public string? EventType { get; set; }

        /// <summary>
        /// Thời điểm thiết bị UHF phát hiện thẻ thực tế tại anten (Hardware Timestamp)
        /// </summary>
        public DateTime? EventTime { get; set; }
    }
}
