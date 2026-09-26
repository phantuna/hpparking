using HPParking.Core.Models.Common;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HPParking.Core.Models.Entities
{
    /// <summary>
    /// Cấu hình tuyến đường di chuyển giữa các cặp cổng và hạn mức thời gian cho phép
    /// </summary>
    [BsonIgnoreExtraElements]
    public class GateRouteConfig : BaseEntity
    {
        /// <summary>
        /// ID Cổng xuất phát (ví dụ: Cổng 1)
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        public string OriginGateId { get; set; } = string.Empty;

        /// <summary>
        /// Mã Cổng xuất phát (ví dụ: GATE_01)
        /// </summary>
        public string OriginGateCode { get; set; } = string.Empty;

        /// <summary>
        /// ID Cổng đích (ví dụ: Cổng 2)
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        public string DestinationGateId { get; set; } = string.Empty;

        /// <summary>
        /// Mã Cổng đích (ví dụ: GATE_02)
        /// </summary>
        public string DestinationGateCode { get; set; } = string.Empty;

        /// <summary>
        /// Thời gian di chuyển tối đa sang cổng đích (tính theo phút, mặc định: 5 phút)
        /// </summary>
        public int MaxTravelToDestMinutes { get; set; } = 5;

        /// <summary>
        /// Thời gian dừng đỗ / bốc dỡ hàng tối đa tại cổng đích (tính theo phút, mặc định: 20 phút)
        /// </summary>
        public int MaxStayAtDestMinutes { get; set; } = 20;

        /// <summary>
        /// Thời gian di chuyển quay về cổng xuất phát tối đa (tính theo phút, mặc định: 5 phút)
        /// </summary>
        public int MaxTravelBackMinutes { get; set; } = 5;

        /// <summary>
        /// Trạng thái hiệu lực của cấu hình tuyến
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
