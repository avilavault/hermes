using AvilaVault.Hermes.Abstractions;

namespace AvilaVault.Hermes.StateMachine;

public sealed class EventContext<TMessage> : IEventContext<TMessage>
{
    public TMessage Message { get; }
    public Guid CorrelationId { get; }
    public DateTimeOffset SentAt { get; }

    public EventContext(TMessage message, Guid correlationId)
    {
        Message = message;
        CorrelationId = correlationId;
        SentAt = DateTimeOffset.UtcNow;
    }
}