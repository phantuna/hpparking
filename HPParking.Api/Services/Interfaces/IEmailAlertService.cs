using HPParking.Api.DTOs.Alerts;

namespace HPParking.Api.Services.Interfaces
{
    public interface IEmailAlertService
    {
        Task SendInterGateTripAlertAsync(InterGateTripAlertModel model, CancellationToken cancellationToken = default);
        Task SendLateCheckInAlertAsync(LateCheckInAlertModel model, CancellationToken cancellationToken = default);
        void EnqueueInterGateAlert(InterGateTripAlertModel model);
        void EnqueueLateCheckInAlert(LateCheckInAlertModel model);
    }
}
