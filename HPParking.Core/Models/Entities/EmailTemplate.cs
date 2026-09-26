using HPParking.Core.Models.Common;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace HPParking.Core.Models.Entities
{
    /// <summary>
    /// Thực thể quản lý mẫu email cảnh báo (Email Template) với các biến động {{placeholder}}
    /// </summary>
    [BsonIgnoreExtraElements]
    public class EmailTemplate : BaseEntity
    {
        /// <summary>
        /// Mã định danh duy nhất của mẫu email (ví dụ: TRIP_DESTINATION_MISSING, TRIP_DESTINATION_TIMEOUT, TRIP_RETURN_TIMEOUT)
        /// </summary>
        public string TemplateCode { get; set; } = string.Empty;

        /// <summary>
        /// Tên hiển thị của mẫu email (ví dụ: Cảnh báo xe quá thời gian di chuyển)
        /// </summary>
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>
        /// Loại cảnh báo liên quan (ví dụ: TRIP_DESTINATION_MISSING, TRIP_DESTINATION_TIMEOUT, TRIP_RETURN_TIMEOUT)
        /// </summary>
        public string AlertType { get; set; } = string.Empty;

        /// <summary>
        /// Tiêu đề email (hỗ trợ {{placeholder}})
        /// </summary>
        public string SubjectTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Nội dung email định dạng HTML (hỗ trợ {{placeholder}})
        /// </summary>
        public string BodyTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Danh sách email người nhận cảnh báo (ví dụ: ["phanductuan07@gmail.com"])
        /// </summary>
        public List<string> Recipients { get; set; } = new List<string>();

        /// <summary>
        /// Trạng thái bật / tắt gửi cảnh báo của mẫu email này
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Danh sách các biến có thể sử dụng được cho mẫu này để gợi ý trên giao diện
        /// </summary>
        public List<string> AvailableVariables { get; set; } = new List<string>();
    }
}
