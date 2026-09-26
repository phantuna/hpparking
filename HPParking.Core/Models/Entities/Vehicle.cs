using HPParking.Core.Models.Common;
using HPParking.Core.Models.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HPParking.Core.Models.Entities
{
    [BsonIgnoreExtraElements]
    public class Vehicle : BaseEntity
    {
        public string PlateNumber { get; set; } = string.Empty;                          // [LƯU DB] Biển số xe (chuỗi chuẩn hóa)

        [BsonRepresentation(BsonType.String)]
        public VehicleType Type { get; set; } = VehicleType.Car;                         // [LƯU DB] Loại xe (Ô tô, Xe máy...)

        [BsonRepresentation(BsonType.ObjectId)]
        public string? OwnerClientId { get; set; }                                       // [LƯU DB] Khóa ngoại liên kết chủ xe (Person)

        public string? TagEpc { get; set; }                                              // [LƯU DB] Mã thẻ RFID/UHF gắn trên xe để tự động nhận diện

        [BsonRepresentation(BsonType.ObjectId)]
        public string? AssignedGateId { get; set; }                                      // [LƯU DB] Cổng thường trực / cổng được gán ban đầu cho xe

        public bool IsActive { get; set; } = true;
    }
}
