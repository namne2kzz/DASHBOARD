using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Contracts;
using MassTransit;

namespace DASHBOARD.Infrastructure.Messaging.Consumers;

/// <summary>MassTransit consumer that handles <see cref="InvitationCreatedMessage"/> and sends the invite email via SMTP.</summary>
public sealed class InvitationCreatedConsumer(IEmailService emailService) : IConsumer<InvitationCreatedMessage>
{
    /// <summary>Sends the invitation email when a new invite is published to the message bus.</summary>
    /// <param name="context">The MassTransit consume context containing the message.</param>
    public async Task Consume(ConsumeContext<InvitationCreatedMessage> context)
    {
        var msg = context.Message;
        await emailService.SendInvitationAsync(
            msg.ToEmail,
            msg.InviteLink,
            msg.InvitedByName,
            msg.ExpiryMinutes,
            context.CancellationToken);
    }
}
