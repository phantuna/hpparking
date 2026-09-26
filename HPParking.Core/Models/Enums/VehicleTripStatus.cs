namespace HPParking.Core.Models.Enums
{
    /// <summary>
    /// Trạng thái của một hành trình di chuyển liên cổng của phương tiện
    /// </summary>
    public enum VehicleTripStatus
    {
        InTransitToDest = 1, // Đang di chuyển sang cổng đích (Chờ CHECK_IN tại cổng đích)
        AtDestination = 2,   // Đã tới cổng đích (Đang bốc dỡ hàng hóa, chờ CHECK_OUT tại cổng đích)
        Returning = 3,       // Đang trên đường quay về cổng xuất phát (Chờ CHECK_IN tại cổng xuất phát)
        Completed = 4,       // Đã hoàn thành hành trình an toàn (Đã CHECK_IN về cổng xuất phát)
        Aborted = 5,         // Hủy chuyến do xe quay đầu về lại cổng xuất phát mà chưa sang cổng đích
        Abnormal = 6         // Bất thường (bỏ sót mốc quét sự kiện hoặc sai lệch luồng)
    }
}
