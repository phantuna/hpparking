using System.Collections.Generic;

namespace HPParking.Api.DTOs.EmailTemplates
{
    public class PreviewEmailTemplateRequest
    {
        public string? TemplateCode { get; set; }
        public string? SubjectTemplate { get; set; }
        public string? BodyTemplate { get; set; }
        public Dictionary<string, string>? CustomVariables { get; set; }
    }

    public class PreviewEmailTemplateResponse
    {
        public string RenderedSubject { get; set; } = string.Empty;
        public string RenderedBody { get; set; } = string.Empty;
        public Dictionary<string, string> VariablesUsed { get; set; } = new Dictionary<string, string>();
    }

    public class TestSendEmailRequest
    {
        /// <summary>
        /// Email nhân sự nhận cảnh báo test (mặc định: phanductuan07@gmail.com)
        /// </summary>
        public string RecipientEmail { get; set; } = "phanductuan07@gmail.com";

        /// <summary>
        /// Mã mẫu email muốn test (mặc định: TRIP_DESTINATION_MISSING)
        /// </summary>
        public string TemplateCode { get; set; } = "TRIP_DESTINATION_MISSING";

        public string? CustomSubject { get; set; }
        public string? CustomBody { get; set; }
        public Dictionary<string, string>? CustomVariables { get; set; }
    }

    public class TestSendEmailResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
    }
}
