namespace AvilaVault.Hermes.StateMachine;

using AvilaVault.Hermes.Abstractions;

/// <summary>
/// O que chega dentro de um .Then() — acesso à saga e à mensagem.
/// </summary>
public sealed class BehaviorContext<TSaga, TMessage>
    where TSaga : class, ISagaState, new()
{
    public TSaga Saga { get; init; } = default!;
    public TMessage Message { get; init; } = default!;
    public Guid CorrelationId { get; init; }
}