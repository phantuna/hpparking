using Asp.Versioning;
using HPParking.Api.DTOs.Auth;
using HPParking.Api.DTOs.Common;
using HPParking.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace HPParking.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class AuthController : BaseApiController
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
            : base(logger)
        {
            _authService = authService;
        }

        /// <summary>
        /// Đăng nhập hệ thống lấy Access Token (15 phút) và Refresh Token (7 ngày), tự động thiết lập HttpOnly Cookie
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("LoginRateLimitPolicy")]
        [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 403)]
        [ProducesResponseType(typeof(ApiResponse<object>), 429)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);

            // Thiết lập HttpOnly Cookie cho Web Admin theo ADR 0026 & ADR 0027
            SetAuthCookies(response.AccessToken, response.RefreshToken, response.ExpiresIn);

            return OkApiResponse(response, "Đăng nhập thành công.");
        }

        /// <summary>
        /// Làm mới phiên đăng nhập (Token Rotation) bằng Refresh Token qua Header hoặc Cookie
        /// </summary>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        [EnableRateLimiting("RefreshTokenRateLimitPolicy")]
        [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        [ProducesResponseType(typeof(ApiResponse<object>), 403)]
        [ProducesResponseType(typeof(ApiResponse<object>), 429)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? bodyRequest = null)
        {
            var refreshTokenFromBody = bodyRequest?.RefreshToken;
            var refreshTokenFromHeader = Request.Headers["X-Refresh-Token"].FirstOrDefault();
            var refreshTokenFromCookie = Request.Cookies["hpparking_refresh_token"];

            // Ưu tiên: Body JSON -> Cookie -> Header
            var token = !string.IsNullOrWhiteSpace(refreshTokenFromBody)
                ? refreshTokenFromBody
                : (!string.IsNullOrWhiteSpace(refreshTokenFromCookie)
                    ? refreshTokenFromCookie
                    : refreshTokenFromHeader);

            if (string.IsNullOrWhiteSpace(token))
            {
                return ErrorApiResponse("Refresh Token không được để trống.", null, StatusCodes.Status401Unauthorized);
            }

            var response = await _authService.RefreshTokenAsync(token, null, HttpContext.RequestAborted);

            // Cập nhật lại cả 2 Cookie sau khi luân chuyển (Token Rotation)
            SetAuthCookies(response.AccessToken, response.RefreshToken, response.ExpiresIn);

            return OkApiResponse(response, "Làm mới token thành công.");
        }

        /// <summary>
        /// Đăng xuất khỏi hệ thống, xóa sạch Cookie và vô hiệu hóa phiên làm việc
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (!string.IsNullOrEmpty(userId))
            {
                await _authService.LogoutAsync(userId);
            }

            // Xóa Cookie trên trình duyệt
            Response.Cookies.Delete("hpparking_access_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });

            Response.Cookies.Delete("hpparking_refresh_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/api/v1/auth/refresh-token"
            });

            return OkApiResponse("Đăng xuất thành công.", "Đăng xuất thành công.");
        }

        private void SetAuthCookies(string accessToken, string? refreshToken, int expiresInSeconds)
        {
            Response.Cookies.Append("hpparking_access_token", accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds),
                Path = "/"
            });

            if (!string.IsNullOrEmpty(refreshToken))
            {
                Response.Cookies.Append("hpparking_refresh_token", refreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(7),
                    Path = "/api/v1/auth/refresh-token"
                });
            }
        }

        /// <summary>
        /// Lấy thông tin tài khoản người dùng đang đăng nhập
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<UserInfoDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userId))
            {
                return ErrorApiResponse("Không xác định được danh tính người dùng trong token.", null, StatusCodes.Status401Unauthorized);
            }

            var userInfo = await _authService.GetCurrentUserAsync(userId);
            return OkApiResponse(userInfo, "Lấy thông tin tài khoản thành công.");
        }

        /// <summary>
        /// Đổi mật khẩu tài khoản người dùng
        /// </summary>
        /// <remarks>
        /// Theo ADR 0025: Mật khẩu mới tối thiểu 6 ký tự. Tài khoản có vai trò Admin được phép đổi mật khẩu mà không cần nhập mật khẩu cũ.
        /// </remarks>
        [HttpPost("change-password")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

            if (string.IsNullOrEmpty(userId))
            {
                return ErrorApiResponse("Không xác định được danh tính người dùng trong token.", null, StatusCodes.Status401Unauthorized);
            }

            await _authService.ChangePasswordAsync(userId, userRole, request);
            return OkApiResponse(true, "Đổi mật khẩu thành công. Vui lòng sử dụng mật khẩu mới cho lần đăng nhập tiếp theo.");
        }
    }
}
