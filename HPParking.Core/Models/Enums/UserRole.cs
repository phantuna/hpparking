namespace HPParking.Core.Models.Enums
{
    /// <summary>
    /// Vai trò của tài khoản người dùng trong hệ thống
    /// </summary>
    public enum UserRole
    {
        Admin = 1,           // Quản trị viên hệ thống
        Manager = 2,         // Quản lý bãi xe
        Viewer = 3,          // Người xem báo cáo
        HR = 4,              // Nhân sự / Giám sát điều phối
    }
}
