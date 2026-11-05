using System.Net;
using System.Net.Mail;
using System.Text;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;

namespace CasamentoTatianaDiogo.Services;

public class RsvpVerificationSender(IConfiguration config) : IRsvpVerificationSender
{
    public bool IsAvailable(RsvpVerificationChannel channel) =>
        channel == RsvpVerificationChannel.Email && config.GetValue<bool>("RsvpVerification:Email:Enabled") &&
        !string.IsNullOrWhiteSpace(config["RsvpEmail:Host"]) && !string.IsNullOrWhiteSpace(config["RsvpEmail:From"]);

    public async Task SendAsync(RsvpVerificationChannel channel, string contact, string code, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable(channel)) throw new InvalidOperationException("Email verification delivery is unavailable.");
        using var message = new MailMessage(config["RsvpEmail:From"]!, contact)
        {
            Subject = "Código de verificação — Tatiana & Diogo",
            Body = $"O teu código de verificação é: {code}\n\nÉ válido durante 10 minutos e só pode ser utilizado uma vez.\nSe não pediste este código, ignora esta mensagem.",
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        using var smtp = new SmtpClient(config["RsvpEmail:Host"], config.GetValue("RsvpEmail:Port", 587))
        {
            EnableSsl = config.GetValue("RsvpEmail:UseSsl", true), Timeout = 15000
        };
        if (!string.IsNullOrWhiteSpace(config["RsvpEmail:Username"]))
            smtp.Credentials = new NetworkCredential(config["RsvpEmail:Username"], config["RsvpEmail:Password"]);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}
