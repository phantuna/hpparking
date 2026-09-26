using HPParking.Api.DTOs.VehicleTrips;
using HPParking.Api.Services.Interfaces;
using HPParking.Core.Interfaces;
using HPParking.Core.Models.Entities;
using HPParking.Core.Models.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HPParking.Api.Services.Implementations
{
    /// <summary>
    /// Tiếp nhận sự kiện quét thẻ UHF từ Gateway/Đầu đọc, thực hiện khử đọc lặp (Debounce) và chuyển tiếp tới State Machine
    /// </summary>
    public class UhfEventIngestionService : IUhfEventIngestionService
    {
        private readonly IRepository<VehicleGateEvent> _eventRepo;
        private readonly IRepository<Vehicle> _vehicleRepo;
        private readonly IRepository<Gate> _gateRepo;
        private readonly IRepository<Device> _deviceRepo;
        private readonly IVehicleTripService _tripService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<UhfEventIngestionService> _logger;

        // Cửa sổ chống đọc lặp UHF: 3 - 10 giây (chuẩn UHF thực tế: 8 giây)
        private const int DebounceSeconds = 8;

        // Cơ chế khóa tuần tự theo phương tiện/thẻ (Concurrency Lock) chống Race Condition khi nhiều Reader bắn cùng mili-giây
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _vehicleLocks = new();

        public UhfEventIngestionService(
            IRepository<VehicleGateEvent> eventRepo,
            IRepository<Vehicle> vehicleRepo,
            IRepository<Gate> gateRepo,
            IRepository<Device> deviceRepo,
            IVehicleTripService tripService,
            IMemoryCache memoryCache,
            ILogger<UhfEventIngestionService> logger)
        {
            _eventRepo = eventRepo;
            _vehicleRepo = vehicleRepo;
            _gateRepo = gateRepo;
            _deviceRepo = deviceRepo;
            _tripService = tripService;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<VehicleGateEvent?> IngestRawEventAsync(UhfRawEventDto rawEvent, CancellationToken cancellationToken = default)
        {
            if (rawEvent == null || string.IsNullOrWhiteSpace(rawEvent.ReaderCode) || string.IsNullOrWhiteSpace(rawEvent.TagEpc))
            {
                _logger.LogWarning("[UhfEventIngestionService] Bỏ qua sự kiện không hợp lệ (thiếu ReaderCode hoặc TagEpc)");
                return null;
            }

            var receivedAt = DateTime.Now;
            var readerCode = rawEvent.ReaderCode.Trim().ToUpperInvariant();
            var tagEpc = rawEvent.TagEpc.Trim().ToUpperInvariant();

            // Khóa đồng thời theo Tag / Xe: đảm bảo 2 request gửi cùng tích tắc cho cùng 1 xe sẽ được xử lý tuần tự tuyệt đối
            var lockKey = !string.IsNullOrWhiteSpace(tagEpc) ? tagEpc : readerCode;
            var semaphore = _vehicleLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                // 1. PHÂN ĐỊNH CHIỀU SỰ KIỆN (CHECK_IN HOẶC CHECK_OUT)
                GateEventType eventType;
                if (!string.IsNullOrWhiteSpace(rawEvent.EventType) && 
                    Enum.TryParse<GateEventType>(rawEvent.EventType, true, out var parsedType))
                {
                    eventType = parsedType;
                }
                else
                {
                    // Fallback theo quy ước đặt tên thiết bị (UHF-C1-OUT -> CheckOut, UHF-C1-IN -> CheckIn)
                    eventType = readerCode.Contains("OUT") ? GateEventType.CheckOut : GateEventType.CheckIn;
                }

                // 2. TÍNH TOÁN THỜI ĐIỂM SỰ KIỆN (Nghiệp vụ tính theo EventTime từ anten, ReceivedAt ghi nhận lúc API nhận)
                var eventTime = rawEvent.EventTime ?? receivedAt;

                // 3. ÁNH XẠ THÔNG TIN CỔNG VÀ THIẾT BỊ
                string? gateId = null;
                string? gateCode = null;
                string? readerId = rawEvent.ReaderId;

                var device = await _deviceRepo.FindOneAsync(d => d.Code == readerCode, cancellationToken);
                if (device != null)
                {
                    readerId ??= device.Id;
                    var allGates = await _gateRepo.GetAllAsync(cancellationToken);
                    var matchedGate = allGates.FirstOrDefault(g => readerCode.Contains(g.Code.ToUpperInvariant()));
                    if (matchedGate != null)
                    {
                        gateId = matchedGate.Id;
                        gateCode = matchedGate.Code;
                    }
                }

                if (string.IsNullOrWhiteSpace(gateCode))
                {
                    var allGates = await _gateRepo.GetAllAsync(cancellationToken);
                    var matchedGate = allGates.FirstOrDefault(g => readerCode.Contains(g.Code.ToUpperInvariant())) 
                                      ?? allGates.FirstOrDefault();
                    if (matchedGate != null)
                    {
                        gateId = matchedGate.Id;
                        gateCode = matchedGate.Code;
                    }
                }

                // 4. ÁNH XẠ THÔNG TIN XE TỪ TAG EPC HOẶC VEHICLE CODE
                string? vehicleId = null;
                string? plateNumber = rawEvent.VehicleCode;

                var vehicle = await _vehicleRepo.FindOneAsync(v => v.TagEpc == tagEpc && v.IsActive, cancellationToken);
                if (vehicle != null)
                {
                    vehicleId = vehicle.Id;
                    plateNumber ??= vehicle.PlateNumber;
                }

                // 5. CƠ CHẾ KHỬ TRÙNG UHF (DEDUPLICATE / DEBOUNCE)
                // Cùng vehicle + cùng gate + cùng event_type trong 3-10 giây -> coi là trùng
                // NGUYÊN TẮC: Vẫn lưu toàn bộ raw event vào DB để audit, nhưng chỉ event hợp lệ đầu tiên mới kích hoạt Trip
                var debounceKey = $"uhf_debounce_{gateId ?? gateCode ?? readerCode}_{tagEpc}_{eventType}";
                bool isDuplicate = _memoryCache.TryGetValue(debounceKey, out _);

                var gateEvent = new VehicleGateEvent
                {
                    ReaderCode = readerCode,
                    ReaderId = readerId,
                    TagEpc = tagEpc,
                    VehicleId = vehicleId,
                    PlateNumber = plateNumber,
                    GateId = gateId,
                    GateCode = gateCode ?? "GATE_UNKNOWN",
                    EventType = eventType,
                    EventTime = eventTime,
                    ReceivedAt = receivedAt,
                    ProcessingResult = isDuplicate ? "Duplicate" : "Valid",
                    IsProcessed = isDuplicate // Nếu là duplicate thì đánh dấu luôn là đã xử lý
                };

                // Lưu raw event vào DB bất biến (Append-Only Audit Log)
                await _eventRepo.AddAsync(gateEvent, cancellationToken);

                if (isDuplicate)
                {
                    _logger.LogInformation("[UhfEventIngestionService] [DEDUPLICATE ĐỌC TRÙNG] Xe {Plate} quét lặp {Event} tại {Gate} trong {Seconds}s. Đã lưu raw event #{Id} (ProcessingResult=Duplicate), bỏ qua State Machine.",
                        plateNumber ?? tagEpc, eventType, gateCode, DebounceSeconds, gateEvent.Id);
                    return gateEvent;
                }

                // Ghi nhận mốc debounce trong 8 giây
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(DebounceSeconds),
                    Priority = CacheItemPriority.Low
                };
                _memoryCache.Set(debounceKey, true, cacheOptions);

                _logger.LogInformation("[UhfEventIngestionService] [SỰ KIỆN HỢP LỆ] Đã ghi nhận Event #{Id}: {Reader} | Xe: {Plate} | Cổng: {Gate} | {Event} lúc {Time:HH:mm:ss} (ReceivedAt: {Received:HH:mm:ss})",
                    gateEvent.Id, readerCode, plateNumber ?? tagEpc, gateCode, eventType, eventTime, receivedAt);

                // 6. CHUYỂN TIẾP DUY NHẤT EVENT HỢP LỆ TỚI XỬ LÝ TRIP
                try
                {
                    await _tripService.ProcessGateEventAsync(gateEvent, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[UhfEventIngestionService] Lỗi khi xử lý State Machine cho Event #{Id}", gateEvent.Id);
                }

                return gateEvent;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
