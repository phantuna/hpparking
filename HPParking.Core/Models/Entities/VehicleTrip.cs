using HPParking.Core.Models.Common;
using HPParking.Core.Models.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace HPParking.Core.Models.Entities
{
    /// <summary>
    /// Thực thể quản lý một hành trình di chuyển liên cổng của phương tiện và các mốc deadline cảnh báo
    /// </summary>
    [BsonIgnoreExtraElements]
    public class VehicleTrip : BaseEntity
    {
        /// <summary>
        /// ID phương tiện thực hiện chuyến
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        public string VehicleId { get; set; } = string.Empty;

        /// <summary>
        /// Biển số phương tiện
        /// </summary>
        public string PlateNumber { get; set; } = string.Empty;

        /// <summary>
        /// ID Cổng xuất phát (Cổng thường trực/được gán)
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        public string OriginGateId { get; set; } = string.Empty;

        /// <summary>
        /// Mã Cổng xuất phát
        /// </summary>
        public string OriginGateCode { get; set; } = string.Empty;

        /// <summary>
        /// ID Cổng đích theo cấu hình tuyến
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        public string DestinationGateId { get; set; } = string.Empty;

        /// <summary>
        /// Mã Cổng đích
        /// </summary>
        public string DestinationGateCode { get; set; } = string.Empty;

        // ================= CÁC MỐC THỜI GIAN THỰC TẾ =================

        /// <summary>
        /// Thời điểm xe CHECK_OUT ra khỏi Cổng xuất phát
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime CheckOutOriginTime { get; set; }

        /// <summary>
        /// Thời điểm xe CHECK_IN vào Cổng đích
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? CheckInDestinationTime { get; set; }

        /// <summary>
        /// Thời điểm xe CHECK_OUT rời khỏi Cổng đích
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? CheckOutDestinationTime { get; set; }

        /// <summary>
        /// Thời điểm xe CHECK_IN quay về lại Cổng xuất phát
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? CheckInOriginTime { get; set; }

        // ================= CÁC MỐC DEADLINE HẠN CHÓT =================

        /// <summary>
        /// Hạn chót xe phải CHECK_IN tại Cổng đích (ví dụ: CheckOutOriginTime + 5 phút)
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime DeadlineCheckInDest { get; set; }

        /// <summary>
        /// Hạn chót xe phải CHECK_OUT rời Cổng đích (ví dụ: CheckInDestinationTime + 20 phút)
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? DeadlineCheckOutDest { get; set; }

        /// <summary>
        /// Hạn chót xe phải CHECK_IN về lại Cổng xuất phát (ví dụ: CheckOutDestinationTime + 5 phút)
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? DeadlineCheckInOrigin { get; set; }

        // ================= TRẠNG THÁI & CỜ CẢNH BÁO EMAIL =================

        /// <summary>
        /// Trạng thái hiện tại của chuyến đi
        /// </summary>
        [BsonRepresentation(BsonType.String)]
        public VehicleTripStatus Status { get; set; } = VehicleTripStatus.InTransitToDest;

        /// <summary>
        /// Cờ đánh dấu đã gửi email cảnh báo quá hạn di chuyển sang Cổng đích (Mốc 1)
        /// </summary>
        public bool AlertMissingCheckInDestSent { get; set; } = false;

        /// <summary>
        /// Thời điểm gửi email cảnh báo Mốc 1
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? AlertMissingCheckInDestSentAt { get; set; }

        /// <summary>
        /// Cờ đánh dấu đã gửi email cảnh báo quá thời gian bốc dỡ hàng tại Cổng đích (Mốc 2)
        /// </summary>
        public bool AlertOverstayDestSent { get; set; } = false;

        /// <summary>
        /// Thời điểm gửi email cảnh báo Mốc 2
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? AlertOverstayDestSentAt { get; set; }

        /// <summary>
        /// Cờ đánh dấu đã gửi email cảnh báo quá hạn quay về Cổng xuất phát (Mốc 3)
        /// </summary>
        public bool AlertMissingCheckInOriginSent { get; set; } = false;

        /// <summary>
        /// Thời điểm gửi email cảnh báo Mốc 3
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? AlertMissingCheckInOriginSentAt { get; set; }

        // ================= CÁC KHOẢNG THỜI GIAN THỰC TẾ TÍNH TOÁN (PHÚT) =================

        /// <summary>
        /// Thời gian di chuyển thực tế từ Cổng xuất phát đến Cổng đích (phút)
        /// </summary>
        public double? TravelToDestMinutes { get; set; }

        /// <summary>
        /// Thời gian dừng đỗ / bốc dỡ thực tế tại Cổng đích (phút)
        /// </summary>
        public double? StayAtDestMinutes { get; set; }

        /// <summary>
        /// Thời gian di chuyển thực tế quay về lại Cổng xuất phát (phút)
        /// </summary>
        public double? TravelBackMinutes { get; set; }

        /// <summary>
        /// Tổng thời gian toàn bộ chuyến đi liên cổng (phút)
        /// </summary>
        public double? TotalTripMinutes { get; set; }
    }
}
