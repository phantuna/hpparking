using HPParking.Api.DTOs.Common;
using HPParking.Core.Models.Enums;
using System;

namespace HPParking.Api.DTOs.VehicleTrips
{
    /// <summary>
    /// DTO đại diện cho một hành trình di chuyển liên cổng của phương tiện
    /// </summary>
    public class VehicleTripDto : AuditableDto
    {
        public string VehicleId { get; set; } = string.Empty;
        public string PlateNumber { get; set; } = string.Empty;

        public string OriginGateId { get; set; } = string.Empty;
        public string OriginGateCode { get; set; } = string.Empty;

        public string DestinationGateId { get; set; } = string.Empty;
        public string DestinationGateCode { get; set; } = string.Empty;

        // Các mốc thời gian thực tế
        public DateTime CheckOutOriginTime { get; set; }
        public DateTime? CheckInDestinationTime { get; set; }
        public DateTime? CheckOutDestinationTime { get; set; }
        public DateTime? CheckInOriginTime { get; set; }

        // Các mốc deadline
        public DateTime DeadlineCheckInDest { get; set; }
        public DateTime? DeadlineCheckOutDest { get; set; }
        public DateTime? DeadlineCheckInOrigin { get; set; }

        // Trạng thái và cờ cảnh báo
        public VehicleTripStatus Status { get; set; }
        public string StatusText { get; set; } = string.Empty;

        public bool AlertMissingCheckInDestSent { get; set; }
        public DateTime? AlertMissingCheckInDestSentAt { get; set; }

        public bool AlertOverstayDestSent { get; set; }
        public DateTime? AlertOverstayDestSentAt { get; set; }

        public bool AlertMissingCheckInOriginSent { get; set; }
        public DateTime? AlertMissingCheckInOriginSentAt { get; set; }

        // Thời gian thực tế tính toán cho từng chặng (phút)
        public double? TravelToDestMinutes { get; set; }
        public double? StayAtDestMinutes { get; set; }
        public double? TravelBackMinutes { get; set; }
        public double? TotalTripMinutes { get; set; }
        public string? Note { get; set; }
    }
}
