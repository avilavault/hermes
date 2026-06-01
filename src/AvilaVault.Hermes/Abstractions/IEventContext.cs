namespace AvilaVault.Hermes.Abstractions;

public interface IEventContext<out TMessage>
{
    TMessage Message { get; }
    Guid CorrelationId { get; }
    DateTimeOffset SentAt { get; }
}