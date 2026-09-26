using System;
using System.Collections.Generic;

namespace HPParking.Api.DTOs.EmailTemplates
{
    /// <summary>
    /// DTO thông tin mẫu email cấu hình
    /// </summary>
    public class EmailTemplateDto
    {
        public string Id { get; set; } = string.Empty;
        public string TemplateCode { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string SubjectTemplate { get; set; } = string.Empty;
        public string BodyTemplate { get; set; } = string.Empty;
        public List<string> Recipients { get; set; } = new List<string>();
        public bool Enabled { get; set; }
        public List<string> AvailableVariables { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
