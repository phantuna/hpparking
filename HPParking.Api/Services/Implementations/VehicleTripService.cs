using HPParking.Api.DTOs.Common;
using HPParking.Api.DTOs.VehicleTrips;
using HPParking.Api.Services.Interfaces;
using HPParking.Core.Interfaces;
using HPParking.Core.Models.Entities;
using HPParking.Core.Models.Enums;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HPParking.Api.Services.Implementations
{
    /// <summary>
    /// Quản lý State Machine vòng đời hành trình xe di chuyển liên cổng và các mốc deadline cảnh báo
    /// </summary>
    public class VehicleTripService : IVehicleTripService
    {
        private readonly IRepository<VehicleTrip> _tripRepo;
        private readonly IRepository<GateRouteConfig> _routeRepo;
        private readonly IRepository<Vehicle> _vehicleRepo;
        private readonly IRepository<Gate> _gateRepo;
        private readonly IRepository<VehicleGateEvent> _eventRepo;
        private readonly IEmailAlertService? _emailAlertService;
        private readonly ILogger<VehicleTripService> _logger;

        public VehicleTripService(
            IRepository<VehicleTrip> tripRepo,
            IRepository<GateRouteConfig> routeRepo,
            IRepository<Vehicle> vehicleRepo,
            IRepository<Gate> gateRepo,
            IRepository<VehicleGateEvent> eventRepo,
            ILogger<VehicleTripService> logger,
            IEmailAlertService? emailAlertService = null)
        {
            _tripRepo = tripRepo;
            _routeRepo = routeRepo;
            _vehicleRepo = vehicleRepo;
            _gateRepo = gateRepo;
            _eventRepo = eventRepo;
            _logger = logger;
            _emailAlertService = emailAlertService;
        }

        public async Task<VehicleTrip?> ProcessGateEventAsync(
            VehicleGateEvent gateEvent,
            CancellationToken cancellationToken = default)
        {
            // 1. Đảm bảo xác định được VehicleId từ Tag EPC nếu event chưa có
            if (string.IsNullOrWhiteSpace(gateEvent.VehicleId))
            {
                if (!string.IsNullOrWhiteSpace(gateEvent.TagEpc))
                {
                    var veh = await _vehicleRepo.FindOneAsync(
                        v => v.TagEpc == gateEvent.TagEpc && v.IsActive,
                        cancellationToken);

                    if (veh != null)
                    {
                        gateEvent.VehicleId = veh.Id;
                        gateEvent.PlateNumber = veh.PlateNumber;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(gateEvent.VehicleId))
            {
                _logger.LogWarning(
                    "[VehicleTripService] Bỏ qua sự kiện vì không xác định được xe từ TagEpc: {TagEpc}",
                    gateEvent.TagEpc);

                gateEvent.IsProcessed = true;
                await _eventRepo.UpdateAsync(gateEvent, cancellationToken);
                return null;
            }

            // 2. Lấy thông tin phương tiện
            var vehicle = await _vehicleRepo.GetByIdAsync(
                gateEvent.VehicleId,
                cancellationToken);
                // 3. Tìm chuyến đi đang hoạt động
            var activeTrips = await _tripRepo.FindAsync(
                t => t.VehicleId == gateEvent.VehicleId &&
                     (t.Status == VehicleTripStatus.InTransitToDest ||
                      t.Status == VehicleTripStatus.AtDestination ||
                      t.Status == VehicleTripStatus.Returning),
                cancellationToken);

            var activeTrip = activeTrips
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault();

            // 4. Xác định cổng "nhà" của xe
            string homeGateId = activeTrip?.OriginGateId
                                ?? vehicle?.AssignedGateId
                                ?? string.Empty;

            string homeGateCode = activeTrip?.OriginGateCode ?? string.Empty;

            if (string.IsNullOrWhiteSpace(homeGateCode) &&
                !string.IsNullOrWhiteSpace(homeGateId))
            {
                var homeGate = await _gateRepo.GetByIdAsync(
                    homeGateId,
                    cancellationToken);

                homeGateCode = homeGate?.Code ?? string.Empty;
            }
            // 5. Kiểm tra event hiện tại có ở Home Gate hay không
            bool isHomeGate = IsHomeGate(
                gateEvent,
                activeTrip,
                homeGateId,
                homeGateCode);

            // Nếu xe chưa từng được cấu hình cổng gán,
            // lần đầu xuất phát sẽ lấy cổng hiện tại làm Home Gate.
            if (string.IsNullOrWhiteSpace(homeGateId) &&
                activeTrip == null)
            {
                homeGateId = gateEvent.GateId ?? string.Empty;
                homeGateCode = gateEvent.GateCode ?? "C1";
            }

            // 6. State Machine chính:
            //    EventType -> CheckOut / CheckIn
            switch (gateEvent.EventType)
            {
                case GateEventType.CheckOut:
                    activeTrip = await HandleCheckOutAsync(
                        gateEvent,
                        activeTrip,
                        isHomeGate,
                        homeGateId,
                        homeGateCode,
                        vehicle,
                        cancellationToken);
                    break;

                case GateEventType.CheckIn:
                    activeTrip = await HandleCheckInAsync(
                        gateEvent,
                        activeTrip,
                        isHomeGate,
                        cancellationToken);
                    break;

                default:
                    _logger.LogWarning(
                        "[VehicleTripService] EventType không được hỗ trợ: {EventType}",
                        gateEvent.EventType);
                    break;
            }

            gateEvent.IsProcessed = true;
            await _eventRepo.UpdateAsync(gateEvent, cancellationToken);

            return activeTrip;
        }
        /// <summary>
        /// Xác định event có diễn ra tại Home Gate của xe hay không.
        /// </summary>
        private static bool IsHomeGate(
            VehicleGateEvent gateEvent,
            VehicleTrip? activeTrip,
            string homeGateId,
            string homeGateCode)
        {
            if (!string.IsNullOrWhiteSpace(homeGateId) &&
                gateEvent.GateId == homeGateId)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(homeGateCode) &&
                !string.IsNullOrWhiteSpace(gateEvent.GateCode) &&
                string.Equals(
                    gateEvent.GateCode.Trim(),
                    homeGateCode.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Chưa cấu hình Home Gate và chưa có Trip:
            // cổng đầu tiên được coi là Home Gate.
            return string.IsNullOrWhiteSpace(homeGateId) &&
                   activeTrip == null;
        }

        /// <summary>
        /// Xử lý toàn bộ CHECK_OUT.
        /// </summary>
        private async Task<VehicleTrip?> HandleCheckOutAsync(
            VehicleGateEvent gateEvent,
            VehicleTrip? activeTrip,
            bool isHomeGate,
            string homeGateId,
            string homeGateCode,
            Vehicle? vehicle,
            CancellationToken cancellationToken)
        {
            // CHECK_OUT tại Home Gate khi chưa có Trip -> mở Trip mới.
            if (activeTrip == null && isHomeGate)
            {
                return await StartNewTripAsync(
                    gateEvent,
                    homeGateId,
                    homeGateCode,
                    vehicle,
                    cancellationToken);
            }
// CHECK_OUT tại cổng phụ khi đang có Trip.
            if (activeTrip != null && !isHomeGate)
            {
                await HandleDestinationCheckOutAsync(
                    gateEvent,
                    activeTrip,
                    cancellationToken);

                return activeTrip;
            }

            _logger.LogWarning(
                "[VehicleTripService] [IGNORED] Xe {Plate} CHECK_OUT tại {Gate} nhưng không khớp trạng thái Home Gate/Trip hiện tại.",
                gateEvent.PlateNumber,
                gateEvent.GateCode);

            return activeTrip;
        }

        /// <summary>
        /// CHECK_OUT tại Home Gate -> bắt đầu một hành trình mới.
        /// </summary>
        private async Task<VehicleTrip> StartNewTripAsync(
            VehicleGateEvent gateEvent,
            string homeGateId,
            string homeGateCode,
            Vehicle? vehicle,
            CancellationToken cancellationToken)
        {
            string originGateId = gateEvent.GateId ?? homeGateId;
            string originGateCode = gateEvent.GateCode ?? homeGateCode;

            int maxTravel = 5;

            var newTrip = new VehicleTrip
            {
                VehicleId = gateEvent.VehicleId,
                PlateNumber = gateEvent.PlateNumber
                              ?? vehicle?.PlateNumber
                              ?? string.Empty,
                OriginGateId = originGateId,
                OriginGateCode = originGateCode,
                CheckOutOriginTime = gateEvent.EventTime,
                DeadlineCheckInDest = gateEvent.EventTime.AddMinutes(maxTravel),
                Status = VehicleTripStatus.InTransitToDest,
                Note = $"Xuất phát từ cổng gán {originGateCode}. Hạn chót Check-in cổng khác: {maxTravel} phút."
            };
 await _tripRepo.AddAsync(newTrip, cancellationToken);

            _logger.LogInformation(
                "[VehicleTripService] Xe {Plate} CHECK_OUT cổng gán {Origin}. Bắt đầu tính giờ 5p đến cổng khác. Hạn chót: {Deadline:HH:mm:ss}",
                newTrip.PlateNumber,
                newTrip.OriginGateCode,
                newTrip.DeadlineCheckInDest);

            return newTrip;
        }

        /// <summary>
        /// CHECK_OUT tại cổng phụ -> bắt đầu tính thời gian quay về Home Gate.
        /// </summary>
        private async Task HandleDestinationCheckOutAsync(
            VehicleGateEvent gateEvent,
            VehicleTrip activeTrip,
            CancellationToken cancellationToken)
        {
            // Chỉ hợp lệ nếu xe đã CHECK_IN cổng phụ nhưng chưa CHECK_OUT.
            if (!activeTrip.CheckInDestinationTime.HasValue ||
                activeTrip.CheckOutDestinationTime.HasValue)
            {
                _logger.LogWarning(
                    "[VehicleTripService] [IGNORED] Xe {Plate} quét CHECK_OUT tại cổng phụ {Gate} nhưng trạng thái không hợp lệ (chưa IN hoặc đã OUT rồi). Bỏ qua.",
                    activeTrip.PlateNumber,
                    gateEvent.GateCode);

                return;
            }

            var stayMinutes = Math.Round(
                (gateEvent.EventTime - activeTrip.CheckInDestinationTime.Value).TotalMinutes,
                2);

            int maxReturn = 5;

            var route = await GetRouteConfigAsync(
                activeTrip.OriginGateId,
                activeTrip.DestinationGateId,
                cancellationToken);

            if (route != null && route.MaxTravelBackMinutes > 0)
            {
                maxReturn = route.MaxTravelBackMinutes;
            }

            activeTrip.CheckOutDestinationTime = gateEvent.EventTime;
            activeTrip.StayAtDestMinutes = stayMinutes;
            activeTrip.DeadlineCheckInOrigin =
                gateEvent.EventTime.AddMinutes(maxReturn);
            activeTrip.Status = VehicleTripStatus.Returning;
            activeTrip.Note =
                $"Rời cổng phụ {gateEvent.GateCode} sau {stayMinutes}p bốc dỡ. " +
                $"Đang quay về cổng gán {activeTrip.OriginGateCode} " +
                $"(Hạn chót: {maxReturn} phút).";

            await _tripRepo.UpdateAsync(activeTrip, cancellationToken);

            _logger.LogInformation(
                "[VehicleTripService] Xe {Plate} CHECK_OUT cổng phụ {Dest} sau {Stay}p. Đang về cổng gán {Origin}. Hạn chót về: {Deadline:HH:mm:ss} ({MaxReturn}p)",
                activeTrip.PlateNumber,
                gateEvent.GateCode,
                stayMinutes,
                activeTrip.OriginGateCode,
                activeTrip.DeadlineCheckInOrigin,
                maxReturn);
        }
                return null;
            }

            // CHECK_IN tại cổng phụ.
            if (!isHomeGate)
            {
                await HandleDestinationCheckInAsync(
                    gateEvent,
                    activeTrip,
                    cancellationToken);

                return activeTrip;
            }

            // CHECK_IN tại Home Gate:
            // phân nhánh theo State Machine.
            switch (activeTrip.Status)
            {
                case VehicleTripStatus.Returning:
                    await CompleteTripAsync(
                        gateEvent,
                        activeTrip,
                        cancellationToken);
                    break;

                case VehicleTripStatus.AtDestination:
                    await HandleAbnormalReturnAsync(
                        gateEvent,
                        activeTrip,
                        cancellationToken);
                    break;

                case VehicleTripStatus.InTransitToDest:
                    await HandleUTurnAsync(
                        gateEvent,
                        activeTrip,
                        cancellationToken);
                    break;

                default:
                    _logger.LogWarning(
                        "[VehicleTripService] CHECK_IN tại Home Gate nhưng trạng thái Trip không hợp lệ: {Status}",
                        activeTrip.Status);
                    break;
            }

            return activeTrip;
        }

        /// <summary>
        /// CHECK_IN lần đầu tại cổng phụ.
        /// Nếu đã CHECK_IN cổng phụ trước đó mà chưa CHECK_OUT:
        /// - cùng cổng -> Duplicate, không reset deadline
        /// - cổng khác -> Unexpected, không đổi Trip
        /// </summary>
        private async Task HandleDestinationCheckInAsync(
            VehicleGateEvent gateEvent,
            VehicleTrip activeTrip,
            CancellationToken cancellationToken)
        {if (activeTrip.CheckInDestinationTime.HasValue &&
                !activeTrip.CheckOutDestinationTime.HasValue)
            {
                if (string.Equals(
                    gateEvent.GateCode,
                    activeTrip.DestinationGateCode,
                    StringComparison.OrdinalIgnoreCase))
                {
                    gateEvent.ProcessingResult = "Duplicate";

                    _logger.LogWarning(
                        "[VehicleTripService] [CHỐNG RESET DEADLINE] Xe {Plate} lại quét CHECK_IN tại {Gate} lúc {Time:HH:mm:ss} khi chưa có CHECK_OUT. KHÔNG reset hạn chót. Hạn chót bốc dỡ vẫn giữ nguyên là {Deadline:HH:mm:ss}",
                        activeTrip.PlateNumber,
                        gateEvent.GateCode,
                        gateEvent.EventTime,
                        activeTrip.DeadlineCheckOutDest);
                }
                else
                {
                    gateEvent.ProcessingResult = "Unexpected";

                    _logger.LogWarning(
                        "[VehicleTripService] ⚠️ [SAI LUỒNG / BẤT THƯỜNG] Xe {Plate} đang tại cổng {CurrentGate} chưa CHECK_OUT nhưng lại quét CHECK_IN tại cổng khác {NewGate} lúc {Time:HH:mm:ss}. Bỏ qua không đổi Trip, giữ nguyên hạn chót {Deadline:HH:mm:ss}",
                        activeTrip.PlateNumber,
                        activeTrip.DestinationGateCode,
                        gateEvent.GateCode,
                        gateEvent.EventTime,
                        activeTrip.DeadlineCheckOutDest);
                }

                return;
            }

            // Lần đầu tiên đến cổng phụ.
            var travelMinutes = Math.Round(
                (gateEvent.EventTime - activeTrip.CheckOutOriginTime).TotalMinutes,
                2);

            activeTrip.DestinationGateId = gateEvent.GateId ?? string.Empty;
            activeTrip.DestinationGateCode = gateEvent.GateCode ?? string.Empty;
            activeTrip.CheckInDestinationTime = gateEvent.EventTime;
            activeTrip.TravelToDestMinutes = travelMinutes;

            int maxStay = 20;

            var route = await GetRouteConfigAsync(
                activeTrip.OriginGateId,
                activeTrip.DestinationGateId,
                cancellationToken);

            if (route != null && route.MaxStayAtDestMinutes > 0)
            {
                maxStay = route.MaxStayAtDestMinutes;
            }

            activeTrip.DeadlineCheckOutDest =
                gateEvent.EventTime.AddMinutes(maxStay);
            activeTrip.Status = VehicleTripStatus.AtDestination;
            activeTrip.Note =
                $"CHECK_IN cổng phụ {activeTrip.DestinationGateCode} sau {travelMinutes}p di chuyển. " +
                $"Bắt đầu tính giờ đỗ bốc dỡ tối đa {maxStay} phút.";

            await _tripRepo.UpdateAsync(activeTrip, cancellationToken);
            logger.LogInformation(
                "[VehicleTripService] Xe {Plate} LẦN ĐẦU CHECK_IN cổng phụ {Dest} (khác cổng gán {Origin}) sau {Travel}p. BẮT ĐẦU TÍNH GIỜ BỐC DỠ ({MaxStay}p). Hạn chót rời cổng: {Deadline:HH:mm:ss}",
                activeTrip.PlateNumber,
                activeTrip.DestinationGateCode,
                activeTrip.OriginGateCode,
                travelMinutes,
                maxStay,
                activeTrip.DeadlineCheckOutDest);
        }

        /// <summary>
        /// Xe đã Returning và CHECK_IN lại Home Gate -> hoàn tất Trip.
        /// </summary>
        private async Task CompleteTripAsync(
            VehicleGateEvent gateEvent,
            VehicleTrip activeTrip,
            CancellationToken cancellationToken)
        {
            var returnMinutes = activeTrip.CheckOutDestinationTime.HasValue
                ? Math.Round(
                    (gateEvent.EventTime - activeTrip.CheckOutDestinationTime.Value).TotalMinutes,
                    2)
                : 0;

            var totalMinutes = Math.Round(
                (gateEvent.EventTime - activeTrip.CheckOutOriginTime).TotalMinutes,
                2);

            activeTrip.CheckInOriginTime = gateEvent.EventTime;
            activeTrip.TravelBackMinutes =
                returnMinutes > 0
                    ? returnMinutes
                    : activeTrip.TravelBackMinutes;
            activeTrip.TotalTripMinutes = totalMinutes;

            activeTrip.Status = VehicleTripStatus.Completed;
            activeTrip.Note =
                $"Hoàn tất hành trình hợp lệ về đúng cổng gán {activeTrip.OriginGateCode}. " +
                $"Chặng đi: {activeTrip.TravelToDestMinutes}p, " +
                $"Đỗ bốc dỡ: {activeTrip.StayAtDestMinutes}p, " +
                $"Chặng về: {activeTrip.TravelBackMinutes}p. " +
                $"Tổng: {totalMinutes}p.";

            await _tripRepo.UpdateAsync(activeTrip, cancellationToken);

            _logger.LogInformation(
                "[VehicleTripService] Xe {Plate} đã CHECK_IN lại Cổng gán {Origin} hợp lệ (đã có Check-out cổng phụ). HOÀN TẤT CHUYẾN ĐI (Tổng {Total}p). ĐÃ NGỪNG TÍNH THỜI GIAN!",
                activeTrip.PlateNumber,
                activeTrip.OriginGateCode,
                totalMinutes);
        }
            {
                _logger.LogError(
                    ex,
                    "[VehicleTripService] Gửi email cảnh báo chuyến đi bất thường thất bại cho xe {Plate}",
                    activeTrip.PlateNumber);
            }
        }

        /// <summary>
        /// Xe vừa CHECK_OUT Home Gate nhưng chưa đến cổng phụ,
        /// sau đó CHECK_IN lại Home Gate -> U-Turn, hủy Trip.
        /// </summary>
        private async Task HandleUTurnAsync(
            VehicleGateEvent gateEvent,
            VehicleTrip activeTrip,
            CancellationToken cancellationToken)
        {
            var earlyMinutes = Math.Round(
                (gateEvent.EventTime - activeTrip.CheckOutOriginTime).TotalMinutes,
                2);

            activeTrip.CheckInOriginTime = gateEvent.EventTime;
            activeTrip.TotalTripMinutes = earlyMinutes;

            activeTrip.Status = VehicleTripStatus.Aborted;
            activeTrip.Note =
                $"Xe quay đầu về lại cổng gán sau {earlyMinutes} phút khi chưa ghé cổng khác. " +
                $"Đã hủy chuyến và dừng tính thời gian.";

            await _tripRepo.UpdateAsync(activeTrip, cancellationToken);

            _logger.LogInformation(
                "[VehicleTripService] Xe {Plate} quay đầu về lại Cổng gán {Origin} sau {Minutes}p. Đóng Trip (Aborted) - ĐÃ NGỪNG TÍNH THỜI GIAN!",
                activeTrip.PlateNumber,
                activeTrip.OriginGateCode,
                earlyMinutes);
        }

        private async Task<GateRouteConfig?> GetRouteConfigAsync(string originGateId, string destGateId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(originGateId) || string.IsNullOrWhiteSpace(destGateId))
            {
                return null;
            }

            var route = await _routeRepo.FindOneAsync(r => r.OriginGateId == originGateId && r.DestinationGateId == destGateId && r.IsActive, cancellationToken);
            if (route != null) return route;

            // Tìm tuyến chiều ngược lại để tái sử dụng định mức
            var reverse = await _routeRepo.FindOneAsync(r => r.OriginGateId == destGateId && r.DestinationGateId == originGateId && r.IsActive, cancellationToken);
            if (reverse != null)
            {
                return new GateRouteConfig
                {
                    OriginGateId = originGateId,
                    DestinationGateId = destGateId,
                    MaxTravelToDestMinutes = reverse.MaxTravelBackMinutes,
                    MaxStayAtDestMinutes = reverse.MaxStayAtDestMinutes,
                    MaxTravelBackMinutes = reverse.MaxTravelToDestMinutes,
                    IsActive = true
                };
            }

            return null;
        }

        private async Task<GateRouteConfig?> GetRouteConfigAsync(string originGateId, string destGateId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(originGateId) || string.IsNullOrWhiteSpace(destGateId))
            {
                return null;
            }

            var route = await _routeRepo.FindOneAsync(r => r.OriginGateId == originGateId && r.DestinationGateId == destGateId && r.IsActive, cancellationToken);
            if (route != null) return route;

            // Tìm tuyến chiều ngược lại để tái sử dụng định mức
            var reverse = await _routeRepo.FindOneAsync(r => r.OriginGateId == destGateId && r.DestinationGateId == originGateId && r.IsActive, cancellationToken);
            if (reverse != null)
            {
                return new GateRouteConfig
                {
                    OriginGateId = originGateId,
                    DestinationGateId = destGateId,
                    MaxTravelToDestMinutes = reverse.MaxTravelBackMinutes,
                    MaxStayAtDestMinutes = reverse.MaxStayAtDestMinutes,
                    MaxTravelBackMinutes = reverse.MaxTravelToDestMinutes,
                    IsActive = true
                };
            }

            return null;
        }

        public async Task<IReadOnlyList<VehicleTrip>> GetActiveTripsAsync(CancellationToken cancellationToken = default)
        {
            return await _tripRepo.FindAsync(
                t => t.Status == VehicleTripStatus.InTransitToDest || 
                     t.Status == VehicleTripStatus.AtDestination || 
                     t.Status == VehicleTripStatus.Returning,
                cancellationToken);
        }

        public async Task<PagedResult<VehicleTripDto>> GetTripsPagedAsync(int pageIndex, int pageSize, string? vehicleId = null, CancellationToken cancellationToken = default)
        {
            var filterBuilder = Builders<VehicleTrip>.Filter;
            var filter = filterBuilder.Empty;

            if (!string.IsNullOrWhiteSpace(vehicleId))
            {
                filter = filterBuilder.Eq(x => x.VehicleId, vehicleId);
            }

            var sort = Builders<VehicleTrip>.Sort.Descending(x => x.CreatedAt);
            int skip = Math.Max(0, (pageIndex - 1) * pageSize);

            var items = await _tripRepo.FindAsync(filter, sort, skip, pageSize, cancellationToken);
            var totalCount = await _tripRepo.CountAsync(t => true, cancellationToken);

            var dtos = items.Select(t => new VehicleTripDto
            {
                Id = t.Id,
                VehicleId = t.VehicleId,
                PlateNumber = t.PlateNumber,
                OriginGateId = t.OriginGateId,
                OriginGateCode = t.OriginGateCode,
                DestinationGateId = t.DestinationGateId,
                DestinationGateCode = t.DestinationGateCode,
                CheckOutOriginTime = t.CheckOutOriginTime,
                CheckInDestinationTime = t.CheckInDestinationTime,
                CheckOutDestinationTime = t.CheckOutDestinationTime,
                CheckInOriginTime = t.CheckInOriginTime,
                DeadlineCheckInDest = t.DeadlineCheckInDest,
                DeadlineCheckOutDest = t.DeadlineCheckOutDest,
                DeadlineCheckInOrigin = t.DeadlineCheckInOrigin,
                Status = t.Status,
                StatusText = GetStatusText(t.Status),
                AlertMissingCheckInDestSent = t.AlertMissingCheckInDestSent,
                AlertMissingCheckInDestSentAt = t.AlertMissingCheckInDestSentAt,
                AlertOverstayDestSent = t.AlertOverstayDestSent,
                AlertOverstayDestSentAt = t.AlertOverstayDestSentAt,
                AlertMissingCheckInOriginSent = t.AlertMissingCheckInOriginSent,
                AlertMissingCheckInOriginSentAt = t.AlertMissingCheckInOriginSentAt,
                TravelToDestMinutes = t.TravelToDestMinutes,
                StayAtDestMinutes = t.StayAtDestMinutes,
                TravelBackMinutes = t.TravelBackMinutes,
                TotalTripMinutes = t.TotalTripMinutes,
                Note = t.Note,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList();

            return new PagedResult<VehicleTripDto>(dtos, pageIndex, pageSize, totalCount);
        }

        public async Task MarkAlertSentAsync(string tripId, int alertMilestone, CancellationToken cancellationToken = default)
        {
            var trip = await _tripRepo.GetByIdAsync(tripId, cancellationToken);
            if (trip == null) return;

            var now = DateTime.Now;
            switch (alertMilestone)
            {
                case 1:
                    trip.AlertMissingCheckInDestSent = true;
                    trip.AlertMissingCheckInDestSentAt = now;
                    break;
                case 2:
                    trip.AlertOverstayDestSent = true;
                    trip.AlertOverstayDestSentAt = now;
                    break;
                case 3:
                    trip.AlertMissingCheckInOriginSent = true;
                    trip.AlertMissingCheckInOriginSentAt = now;
                    break;
            }

            await _tripRepo.UpdateAsync(trip, cancellationToken);
        }

        private static string GetStatusText(VehicleTripStatus status)
        {
            return status switch
            {
                VehicleTripStatus.InTransitToDest => "Đang sang cổng đích",
                VehicleTripStatus.AtDestination => "Đang tại cổng đích",
                VehicleTripStatus.Returning => "Đang quay về",
                VehicleTripStatus.Completed => "Hoàn thành",
                VehicleTripStatus.Aborted => "Đã hủy (Quay đầu)",
                VehicleTripStatus.Abnormal => "Bất thường",
                _ => "Không xác định"
            };
        }
    }
}
