using Asp.Versioning;
using HPParking.Api.DTOs.Common;
using HPParking.Api.DTOs.EmailTemplates;
using HPParking.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HPParking.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/email-templates")]
    public class EmailTemplatesController : BaseApiController
    {
        private readonly IEmailTemplateService _templateService;

        public EmailTemplatesController(
            IEmailTemplateService templateService,
            ILogger<EmailTemplatesController> logger)
            : base(logger)
        {
            _templateService = templateService;
        }

        /// <summary>
        /// Lấy toàn bộ danh sách các mẫu email cảnh báo trong hệ thống
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmailTemplateDto>>), 200)]
        public async Task<IActionResult> GetAllTemplates()
        {
            var templates = await _templateService.GetAllTemplatesAsync(HttpContext.RequestAborted);
            return OkApiResponse(templates, "Lấy danh sách mẫu email thành công.");
        }

        /// <summary>
        /// Lấy chi tiết một mẫu email theo Id
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<EmailTemplateDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        public async Task<IActionResult> GetTemplateById(string id)
        {
            var template = await _templateService.GetTemplateByIdAsync(id, HttpContext.RequestAborted);
            if (template == null)
            {
                return ErrorApiResponse($"Không tìm thấy mẫu email với ID: {id}", statusCode: StatusCodes.Status404NotFound);
            }
            return OkApiResponse(template, "Lấy chi tiết mẫu email thành công.");
        }

        /// <summary>
        /// Lấy chi tiết một mẫu email theo Mã code (ví dụ: TRIP_DESTINATION_MISSING)
        /// </summary>
        [HttpGet("code/{code}")]
        [ProducesResponseType(typeof(ApiResponse<EmailTemplateDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        public async Task<IActionResult> GetTemplateByCode(string code)
        {
            var template = await _templateService.GetTemplateByCodeAsync(code, HttpContext.RequestAborted);
            if (template == null)
            {
                return ErrorApiResponse($"Không tìm thấy mẫu email với mã code: {code}", statusCode: StatusCodes.Status404NotFound);
            }
            return OkApiResponse(template, "Lấy chi tiết mẫu email theo mã code thành công.");
        }

        /// <summary>
        /// Tạo mới một mẫu email cảnh báo
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<EmailTemplateDto>), 201)]
        public async Task<IActionResult> CreateTemplate([FromBody] CreateEmailTemplateRequest request)
        {
            var result = await _templateService.CreateTemplateAsync(request, HttpContext.RequestAborted);
            return CreatedApiResponse(result, "Tạo mới mẫu email thành công.");
        }

        /// <summary>
        /// Cập nhật cấu hình mẫu email (tiêu đề, nội dung HTML, người nhận, trạng thái hoạt động)
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse<EmailTemplateDto>), 200)]
        public async Task<IActionResult> UpdateTemplate(string id, [FromBody] UpdateEmailTemplateRequest request)
        {
            var result = await _templateService.UpdateTemplateAsync(id, request, HttpContext.RequestAborted);
            return OkApiResponse(result, "Cập nhật mẫu email thành công.");
        }

        /// <summary>
        /// Xóa mềm một mẫu email
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        public async Task<IActionResult> DeleteTemplate(string id)
        {
            var result = await _templateService.DeleteTemplateAsync(id, HttpContext.RequestAborted);
            return OkApiResponse(result, "Xóa mẫu email thành công.");
        }

        /// <summary>
        /// Xem trước (Preview) nội dung email sau khi thế các biến động {{placeholder}}
        /// </summary>
        [HttpPost("preview")]
        [ProducesResponseType(typeof(ApiResponse<PreviewEmailTemplateResponse>), 200)]
        public async Task<IActionResult> PreviewTemplate([FromBody] PreviewEmailTemplateRequest request)
        {
            var result = await _templateService.PreviewTemplateAsync(request, HttpContext.RequestAborted);
            return OkApiResponse(result, "Xem trước mẫu email thành công.");
        }

        /// <summary>
        /// Bắn thử nghiệm một email cảnh báo từ địa chỉ vnplus570@gmail.com sang email nhân sự (mặc định: phanductuan07@gmail.com)
        /// </summary>
        [HttpPost("test-send")]
        [ProducesResponseType(typeof(ApiResponse<TestSendEmailResponse>), 200)]
        public async Task<IActionResult> TestSendEmail([FromBody] TestSendEmailRequest request)
        {
            var result = await _templateService.TestSendEmailAsync(request, HttpContext.RequestAborted);
            return OkApiResponse(result, result.Success ? "Gửi email thử nghiệm thành công!" : "Gửi email thử nghiệm thất bại!");
        }

        /// <summary>
        /// Lấy danh sách các biến động {{...}} có thể sử dụng được khi viết template
        /// </summary>
        [HttpGet("variables")]
        [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<string>>), 200)]
        public IActionResult GetVariables([FromQuery] string? templateCode = null)
        {
            var vars = _templateService.GetAvailableVariables(templateCode);
            return OkApiResponse(vars, "Lấy danh sách biến mẫu email thành công.");
        }
    }
}
