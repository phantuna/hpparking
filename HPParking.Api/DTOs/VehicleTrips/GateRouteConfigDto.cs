using HPParking.Api.DTOs.Common;

namespace HPParking.Api.DTOs.VehicleTrips
{
    public class GateRouteConfigDto : AuditableDto
    {
        public string OriginGateId { get; set; } = string.Empty;
        public string OriginGateCode { get; set; } = string.Empty;
        public string DestinationGateId { get; set; } = string.Empty;
        public string DestinationGateCode { get; set; } = string.Empty;

        public int MaxTravelToDestMinutes { get; set; } = 5;
        public int MaxStayAtDestMinutes { get; set; } = 20;
        public int MaxTravelBackMinutes { get; set; } = 5;

        public bool IsActive { get; set; } = true;
    }

    public class CreateOrUpdateGateRouteConfigRequest
    {
        public string OriginGateId { get; set; } = string.Empty;
        public string DestinationGateId { get; set; } = string.Empty;
        public int MaxTravelToDestMinutes { get; set; } = 5;
        public int MaxStayAtDestMinutes { get; set; } = 20;
        public int MaxTravelBackMinutes { get; set; } = 5;
        public bool IsActive { get; set; } = true;
    }
}
