using HPParking.Api.DTOs.EmailTemplates;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HPParking.Api.Services.Interfaces
{
    public interface IEmailTemplateService
    {
        Task<IReadOnlyList<EmailTemplateDto>> GetAllTemplatesAsync(CancellationToken cancellationToken = default);
        Task<EmailTemplateDto?> GetTemplateByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<EmailTemplateDto?> GetTemplateByCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<EmailTemplateDto> CreateTemplateAsync(CreateEmailTemplateRequest request, CancellationToken cancellationToken = default);
        Task<EmailTemplateDto> UpdateTemplateAsync(string id, UpdateEmailTemplateRequest request, CancellationToken cancellationToken = default);
        Task<bool> DeleteTemplateAsync(string id, CancellationToken cancellationToken = default);
        Task<PreviewEmailTemplateResponse> PreviewTemplateAsync(PreviewEmailTemplateRequest request, CancellationToken cancellationToken = default);
        Task<TestSendEmailResponse> TestSendEmailAsync(TestSendEmailRequest request, CancellationToken cancellationToken = default);
        IReadOnlyList<string> GetAvailableVariables(string? templateCode = null);
        string RenderTemplate(string template, IDictionary<string, string> variables);
    }
}
