using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HPParking.Api.DTOs.EmailTemplates
{
    public class CreateEmailTemplateRequest
    {
        [Required(ErrorMessage = "Mã mẫu email (TemplateCode) là bắt buộc.")]
        public string TemplateCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên mẫu email (TemplateName) là bắt buộc.")]
        public string TemplateName { get; set; } = string.Empty;

        public string AlertType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tiêu đề email (SubjectTemplate) là bắt buộc.")]
        public string SubjectTemplate { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nội dung email (BodyTemplate) là bắt buộc.")]
        public string BodyTemplate { get; set; } = string.Empty;

        public List<string> Recipients { get; set; } = new List<string>();

        public bool Enabled { get; set; } = true;
    }

    public class UpdateEmailTemplateRequest
    {
        public string? TemplateName { get; set; }

        public string? AlertType { get; set; }

        public string? SubjectTemplate { get; set; }

        public string? BodyTemplate { get; set; }

        public List<string>? Recipients { get; set; }

        public bool? Enabled { get; set; }
    }
}
