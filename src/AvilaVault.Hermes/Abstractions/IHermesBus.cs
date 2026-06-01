namespace AvilaVault.Hermes.Abstractions;

public interface IHermesBus
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken ct = default)
        where TMessage : class, ICorrelatedMessage;
}