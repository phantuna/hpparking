namespace HPParking.Api.DTOs.Auth
{
    /// <summary>
    /// Yêu cầu làm mới phiên đăng nhập bằng Refresh Token qua Body JSON
    /// </summary>
    public class RefreshTokenRequest
    {
        /// <summary>
        /// Chuỗi Refresh Token JWT (nếu không dùng Cookie hoặc Header)
        /// </summary>
        public string? RefreshToken { get; set; }
    }
}
