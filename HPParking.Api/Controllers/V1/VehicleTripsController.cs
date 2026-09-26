using Asp.Versioning;
using HPParking.Api.DTOs.Common;
using HPParking.Api.DTOs.VehicleTrips;
using HPParking.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HPParking.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/vehicle-trips")]
    public class VehicleTripsController : BaseApiController
    {
        private readonly IVehicleTripService _tripService;

        public VehicleTripsController(
            IVehicleTripService tripService,
            ILogger<VehicleTripsController> logger)
            : base(logger)
        {
            _tripService = tripService;
        }

        /// <summary>
        /// Lấy danh sách lịch sử hành trình xe nâng liên cổng có phân trang và lọc
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<VehicleTripDto>>), 200)]
        public async Task<IActionResult> GetTrips(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? vehicleId = null)
        {
            var result = await _tripService.GetTripsPagedAsync(pageIndex, pageSize, vehicleId, HttpContext.RequestAborted);
            return OkApiResponse(result, "Lấy danh sách hành trình xe nâng thành công.");
        }

        /// <summary>
        /// Lấy danh sách các hành trình xe đang di chuyển thực tế (chưa kết thúc)
        /// Phục vụ cho Dashboard giám sát thời gian thực
        /// </summary>
        [HttpGet("active")]
        [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<object>>), 200)]
        public async Task<IActionResult> GetActiveTrips()
        {
            var activeTrips = await _tripService.GetActiveTripsAsync(HttpContext.RequestAborted);
            return OkApiResponse(activeTrips, "Lấy danh sách hành trình đang hoạt động thành công.");
        }
    }
}
