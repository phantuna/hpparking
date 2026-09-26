using HPParking.Core.Models.Common;
using HPParking.Core.Models.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace HPParking.Core.Models.Entities
{
    /// <summary>
    /// Bản ghi sự kiện quét thẻ UHF tại Cổng (Dữ liệu Raw bất biến - Append Only)
    /// </summary>
    [BsonIgnoreExtraElements]
    public class VehicleGateEvent : BaseEntity
    {
        /// <summary>
        /// Mã định danh của đầu đọc UHF phát hiện sự kiện (ví dụ: UHF-C1-OUT, UHF-C2-IN)
        /// </summary>
        public string ReaderCode { get; set; } = string.Empty;

        /// <summary>
        /// ID thiết bị đầu đọc trong cơ sở dữ liệu (tùy chọn)
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        public string? ReaderId { get; set; }

        /// <summary>
        /// Mã thẻ EPC RFID/UHF gắn trên phương tiện
        /// </summary>
        public string TagEpc { get; set; } = string.Empty;

        /// <summary>
        /// ID phương tiện (ánh xạ từ TagEpc)
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        public string? VehicleId { get; set; }

        /// <summary>
        /// Biển số phương tiện tại thời điểm ghi nhận sự kiện
        /// </summary>
        public string? PlateNumber { get; set; }

        /// <summary>
        /// ID Cổng kiểm soát (ánh xạ từ ReaderCode hoặc cấu hình phần cứng)
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        public string? GateId { get; set; }

        /// <summary>
        /// Mã Cổng kiểm soát (ví dụ: GATE_01, GATE_02)
        /// </summary>
        public string? GateCode { get; set; }

        /// <summary>
        /// Chiều sự kiện: CheckIn (Xe vào cổng) hoặc CheckOut (Xe ra khỏi cổng)
        /// </summary>
        [BsonRepresentation(BsonType.String)]
        public GateEventType EventType { get; set; } = GateEventType.CheckIn;

        /// <summary>
        /// Thời điểm thiết bị đầu đọc UHF phát hiện thẻ thực tế (Hardware Detection Timestamp)
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime EventTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Thời điểm Backend API nhận được dữ liệu (Server Reception Timestamp)
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime ReceivedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Kết quả xử lý sự kiện:
        /// - "Valid": Sự kiện hợp lệ, được chuyển tiếp xử lý Trip
        /// - "Duplicate": Sự kiện đọc trùng/quét lặp trong debounce window (chỉ lưu raw, không xử lý lại)
        /// - "Unexpected": Sự kiện sai luồng (ví dụ: đang ở C2 chưa OUT mà quét IN ở C3)
        /// - "Ignored": Bỏ qua do không xác định được xe/thẻ
        /// </summary>
        public string ProcessingResult { get; set; } = "Valid";

        /// <summary>
        /// Đánh dấu sự kiện này đã được xử lý bởi State Machine của VehicleTrip hay chưa
        /// </summary>
        public bool IsProcessed { get; set; } = false;
    }
}
