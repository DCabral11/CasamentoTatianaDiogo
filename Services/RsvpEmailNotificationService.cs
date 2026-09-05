using System.Net;
using System.Net.Mail;
using System.Text;
using CasamentoTatianaDiogo.Services.Interfaces;

namespace CasamentoTatianaDiogo.Services
{
    public class RsvpEmailNotificationService(IConfiguration configuration, ILogger<RsvpEmailNotificationService> logger) : IRsvpEmailNotificationService
    {
        public async Task SendAsync(IReadOnlyCollection<RsvpEmailDetail> responses)
        {
            if (!bool.TryParse(configuration["RsvpEmail:Enabled"], out var enabled) || !enabled)
            {
                logger.LogInformation("RSVP email notification skipped because it is disabled.");
                return;
            }

            var host = configuration["RsvpEmail:Host"];
            var from = configuration["RsvpEmail:From"];
            var recipient = configuration["RsvpEmail:Recipient"] ?? "diogotita.casamento@gmail.com";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            {
                logger.LogError("RSVP email notification is enabled but SMTP Host or From is missing.");
                return;
            }

            var body = new StringBuilder("Foi recebida uma atualização de confirmação de presença:\n");

            foreach (var response in responses)
            {
                body.AppendLine();
                body.AppendLine($"Convidado: {response.GuestName}");
                body.AppendLine($"Presença: {response.Status}");

                if (!string.IsNullOrWhiteSpace(response.PlusOneName))
                    body.AppendLine($"Acompanhante: {response.PlusOneName}");
                if (!string.IsNullOrWhiteSpace(response.DietaryRestrictions))
                    body.AppendLine($"Restrições alimentares: {response.DietaryRestrictions}");
                if (!string.IsNullOrWhiteSpace(response.MusicRequest))
                    body.AppendLine($"Sugestão musical: {response.MusicRequest}");
                if (!string.IsNullOrWhiteSpace(response.Message))
                    body.AppendLine($"Mensagem: {response.Message}");
                if (!string.IsNullOrWhiteSpace(response.PlusOneDietaryRestrictions))
                    body.AppendLine($"Restrições alimentares do/a acompanhante: {response.PlusOneDietaryRestrictions}");
                if (!string.IsNullOrWhiteSpace(response.PlusOneMusicRequest))
                    body.AppendLine($"Sugestão musical do/a acompanhante: {response.PlusOneMusicRequest}");
                if (!string.IsNullOrWhiteSpace(response.PlusOneMessage))
                    body.AppendLine($"Mensagem do/a acompanhante: {response.PlusOneMessage}");
            }

            try
            {
                using var message = new MailMessage(from, recipient, "Atualização de RSVP", body.ToString());
                using var client = new SmtpClient(host, configuration.GetValue("RsvpEmail:Port", 587))
                {
                    EnableSsl = configuration.GetValue("RsvpEmail:UseSsl", true)
                };

                var username = configuration["RsvpEmail:Username"];
                var password = configuration["RsvpEmail:Password"];

                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                    client.Credentials = new NetworkCredential(username, password);

                await client.SendMailAsync(message);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not send RSVP notification email.");
            }
        }
    }
}
