namespace HPParking.Core.Models.Enums
{
    public enum DeviceType
    {
        Camera = 1,       // Camera (Biển số / Toàn cảnh)
        Controller = 2,   // Bộ điều khiển Barie (ZKTeco / C3-400 / Inbio)
        FaceId = 3,       // Máy nhận diện khuôn mặt FaceID
        Other = 4,        // Thiết bị khác
        UhfReader = 5     // Đầu đọc thẻ tầm xa UHF RFID (Check-in/Check-out xe)
    }
}
