using AvilaVault.Hermes.Abstractions;

namespace AvilaVault.Hermes.StateMachine;

/// <summary>
/// Base class para todas as sagas do Hermes.
/// Derive desta classe, declare seus State e Event<T> como propriedades,
/// e configure as transições no construtor usando Initially() / During() / When() / Then() / TransitionTo().
/// </summary>
public abstract class HermesStateMachine<TSaga>
    where TSaga : class, ISagaState, new()
{
    // Estados reservados (igual ao MassTransit)
    public State Initial { get; } = new("Initial");
    public State Final { get; } = new("Final");

    // Registro interno: (estadoAtual, tipoDoEvento) → TransitionDefinition
    private readonly Dictionary<(string state, Type eventType), TransitionDefinition<TSaga>> _transitions = new();

    // ── DSL ─────────────────────────────────────────────────────────────────

    protected State DefineState(string name) => new(name);

    protected Event<TMessage> DefineEvent<TMessage>() => new();

    protected IStateConfigurator<TSaga> Initially()
        => new StateConfigurator<TSaga>(Initial.Name, _transitions);

    protected IStateConfigurator<TSaga> During(params State[] states)
        => new StateConfigurator<TSaga>(_transitions, states.Select(s => s.Name).ToArray());

    // ── Dispatch (chamado pelo SagaMessageHandler) ───────────────────────────

    internal async Task DispatchAsync<TMessage>(
        TSaga saga,
        IEventContext<TMessage> context,
        CancellationToken ct)
        where TMessage : class
    {
        var key = (saga.CurrentState, typeof(TMessage));

        if (!_transitions.TryGetValue(key, out var transition))
            return; // evento ignorado neste estado — comportamento intencional

        await transition.ExecuteAsync(saga, context, ct);
    }

    internal IEnumerable<Type> GetRegisteredEventTypes()
        => _transitions.Keys.Select(k => k.eventType).Distinct();
}