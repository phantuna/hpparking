namespace HPParking.Core.Models.Enums
{
    /// <summary>
    /// Phân loại sự kiện ghi nhận từ đầu đọc UHF tại Cổng kiểm soát
    /// </summary>
    public enum GateEventType
    {
        CheckIn = 1,   // Quét phương tiện vào cổng
        CheckOut = 2   // Quét phương tiện ra khỏi cổng
    }
}
