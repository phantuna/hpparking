namespace HPParking.Api.Configuration
{
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = "HPParking Security Alert";
        public string AppPassword { get; set; } = string.Empty;
        public bool EnableSsl { get; set; } = true;
        public List<string> AlertRecipients { get; set; } = new();
    }

    public class GateTripSettings
    {
        public int MaxTravelMinutes { get; set; } = 15;
        public int MaxStayMinutes { get; set; } = 30;
    }

    public class WorkShiftSettings
    {
        public string ShiftStartTime { get; set; } = "08:00";
        public int GracePeriodMinutes { get; set; } = 15;
    }
}
