using Asp.Versioning;
using HPParking.Api.DTOs.Common;
using HPParking.Api.DTOs.VehicleTrips;
using HPParking.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace HPParking.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/uhf-events")]
    public class UhfEventsController : BaseApiController
    {
        private readonly IUhfEventIngestionService _ingestionService;

        public UhfEventsController(
            IUhfEventIngestionService ingestionService,
            ILogger<UhfEventsController> logger)
            : base(logger)
        {
            _ingestionService = ingestionService;
        }

        /// <summary>
        /// API tiếp nhận sự kiện quét thẻ RFID/UHF từ Gateway phần cứng hoặc đầu đọc trạm cổng
        /// Tự động lọc trùng (Debounce 45s), ghi nhận Raw Event và cập nhật State Machine hành trình xe
        /// </summary>
        [HttpPost("ingest")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        public async Task<IActionResult> IngestEvent([FromBody] UhfRawEventDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ReaderCode) || string.IsNullOrWhiteSpace(request.TagEpc))
            {
                return ErrorApiResponse("Dữ liệu sự kiện UHF không hợp lệ (yêu cầu bắt buộc ReaderCode và TagEpc).");
            }

            var result = await _ingestionService.IngestRawEventAsync(request, HttpContext.RequestAborted);
            if (result == null)
            {
                return OkApiResponse(new 
                { 
                    Ignored = true, 
                    Reason = "Đã bỏ qua do trùng lặp trong thời gian Cooldown (Debounce 45s)" 
                }, "Sự kiện quét thẻ đã được tiếp nhận và xử lý lọc chống lặp thành công.");
            }

            return OkApiResponse(new
            {
                EventId = result.Id,
                result.ReaderCode,
                result.TagEpc,
                result.PlateNumber,
                result.GateCode,
                EventType = result.EventType.ToString(),
                result.EventTime
            }, "Ghi nhận và xử lý sự kiện UHF thành công.");
        }
    }
}
