using CasamentoTatianaDiogo.Models;

namespace CasamentoTatianaDiogo.Services.Interfaces;

public interface IRsvpVerificationSender
{
    bool IsAvailable(RsvpVerificationChannel channel);
    Task SendAsync(RsvpVerificationChannel channel, string contact, string code, CancellationToken cancellationToken = default);
}
