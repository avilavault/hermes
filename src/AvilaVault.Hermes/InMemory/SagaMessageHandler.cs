using AvilaVault.Hermes.Abstractions;
using AvilaVault.Hermes.StateMachine;
using Microsoft.Extensions.Logging;

namespace AvilaVault.Hermes.InMemory;

/// <summary>
/// Handler registrado no DI para cada (TStateMachine, TMessage).
/// Resolve ou cria a saga, faz o dispatch para a machine e persiste.
/// </summary>
internal sealed class SagaMessageHandler<TStateMachine, TSaga, TMessage>
    : ISagaMessageHandler<TMessage>
    where TStateMachine : HermesStateMachine<TSaga>
    where TSaga : class, ISagaState, new()
    where TMessage : class, ICorrelatedMessage
{
    private readonly TStateMachine _machine;
    private readonly ISagaRepository<TSaga> _repository;
    private readonly ILogger<SagaMessageHandler<TStateMachine, TSaga, TMessage>> _logger;

    public SagaMessageHandler(
        TStateMachine machine,
        ISagaRepository<TSaga> repository,
        ILogger<SagaMessageHandler<TStateMachine, TSaga, TMessage>> logger)
    {
        _machine = machine;
        _repository = repository;
        _logger = logger;
    }

    public async Task HandleAsync(TMessage message, CancellationToken ct = default)
    {
        var correlationId = message.CorrelationId;

        var saga = await _repository.GetAsync(correlationId, ct)
                   ?? new TSaga
                   {
                       CorrelationId = correlationId,
                       CurrentState = "Initial"
                   };

        var previousState = saga.CurrentState;
        var context = new EventContext<TMessage>(message, correlationId);

        _logger.LogDebug(
            "[Hermes] {Machine} | CorrelationId={CorrelationId} | State={State} | Event={Event}",
            typeof(TStateMachine).Name, correlationId, previousState, typeof(TMessage).Name);

        await _machine.DispatchAsync(saga, context, ct);

        if (saga.CurrentState != previousState || previousState == "Initial")
        {
            _logger.LogDebug(
                "[Hermes] Transition: {From} → {To}",
                previousState, saga.CurrentState);

            await _repository.SaveAsync(saga, ct);
        }

        if (saga.CurrentState == "Final")
            await _repository.DeleteAsync(correlationId, ct);
    }
}