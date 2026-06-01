using AvilaVault.Hermes.Abstractions;

namespace AvilaVault.Hermes.StateMachine;

internal sealed class TransitionDefinition<TSaga>
    where TSaga : class, ISagaState, new()
{
    private readonly List<Func<TSaga, object, Guid, Task>> _actions = new();
    private string? _targetState;

    internal void AddAction<TMessage>(Func<BehaviorContext<TSaga, TMessage>, Task> action)
    {
        _actions.Add(async (saga, msg, correlationId) =>
            await action(new BehaviorContext<TSaga, TMessage>
            {
                Saga = saga,
                Message = (TMessage)msg,
                CorrelationId = correlationId
            }));
    }

    internal void SetTargetState(string stateName) =>
        _targetState = stateName;

    internal async Task ExecuteAsync<TMessage>(
        TSaga saga,
        IEventContext<TMessage> ctx,
        CancellationToken ct)
    {
        foreach (var action in _actions)
            await action(saga, ctx.Message!, ctx.CorrelationId);

        if (_targetState is not null)
            saga.CurrentState = _targetState;
    }
}