namespace HPParking.Api.Configuration
{
    /// <summary>
    /// Cấu hình SMTP và danh sách email nhận cảnh báo từ hệ thống
    /// </summary>
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = "alert@hpparking.vn";
        public string FromName { get; set; } = "HPParking Alert System";
        public string HrRecipients { get; set; } = "hr@example.com";
    }
}
