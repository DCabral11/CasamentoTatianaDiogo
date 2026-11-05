using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class RsvpVerificationSenderTests
{
    [Fact]
    public void VerificationEmailCanBeEnabledIndependentlyOfNotificationsToTheCouple()
    {
        var sender = new RsvpVerificationSender(Config(new()
        {
            ["RsvpVerification:Email:Enabled"] = "true", ["RsvpEmail:Enabled"] = "false",
            ["RsvpEmail:Host"] = "smtp.gmail.com", ["RsvpEmail:From"] = "sender@example.test"
        }));
        Assert.True(sender.IsAvailable(RsvpVerificationChannel.Email));
    }

    [Fact]
    public async Task MissingConfigurationCannotPretendToDeliverACode()
    {
        var sender = new RsvpVerificationSender(Config(new()));
        Assert.False(sender.IsAvailable(RsvpVerificationChannel.Email));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(RsvpVerificationChannel.Email, "ana@example.test", "123456"));
    }

    [Fact]
    public async Task NonEmailDeliveryCannotBeEnabledByOldConfiguration()
    {
        var sender = new RsvpVerificationSender(Config(new()
        {
            ["RsvpVerification:Email:Enabled"] = "true", ["RsvpEmail:Host"] = "smtp.gmail.com", ["RsvpEmail:From"] = "sender@example.test",
            ["RsvpVerification:Sms:Enabled"] = "true"
        }));
        Assert.False(sender.IsAvailable((RsvpVerificationChannel)1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync((RsvpVerificationChannel)1, "+351912345678", "123456"));
    }

    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
