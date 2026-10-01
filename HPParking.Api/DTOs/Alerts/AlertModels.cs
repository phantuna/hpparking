namespace HPParking.Api.DTOs.Alerts
{
    public class InterGateTripAlertModel
    {
        public string PlateNumber { get; set; } = string.Empty;
        public string? ClientName { get; set; }
        public string? DepartmentName { get; set; }
        public string OriginGateName { get; set; } = string.Empty;
        public string DestinationGateName { get; set; } = string.Empty;
        public DateTime CheckOutOriginTime { get; set; }
        public DateTime CheckInDestTime { get; set; }
        public double ActualTravelMinutes { get; set; }
        public int MaxAllowedMinutes { get; set; }
        public double OverdueMinutes => Math.Max(0, ActualTravelMinutes - MaxAllowedMinutes);
        public string AlertReason { get; set; } = "Di chuyển liên cổng quá thời gian quy định";
        public string? OverviewImagePath { get; set; }
    }

    public class LateCheckInAlertModel
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string? EmployeeCode { get; set; }
        public string? DepartmentName { get; set; }
        public string? PlateNumber { get; set; }
        public string GateName { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; }
        public string ShiftStartTime { get; set; } = "08:00";
        public int LateMinutes { get; set; }
        public string? OverviewImagePath { get; set; }
    }
}
