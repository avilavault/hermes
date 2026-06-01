namespace AvilaVault.Hermes.Abstractions;

/// <summary>
/// Handler interno registrado no DI para cada (StateMachine, TMessage).
/// O bus resolve todos os handlers para o tipo da mensagem e os invoca.
/// </summary>
internal interface ISagaMessageHandler<in TMessage>
    where TMessage : class, ICorrelatedMessage
{
    Task HandleAsync(TMessage message, CancellationToken ct = default);
}