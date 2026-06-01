using AvilaVault.Hermes.Abstractions;

namespace AvilaVault.Hermes.StateMachine;

public interface IStateConfigurator<TSaga>
    where TSaga : class, ISagaState, new()
{
    IBehaviorBuilder<TSaga, TMessage> When<TMessage>(Event<TMessage> @event)
        where TMessage : class, ICorrelatedMessage;
}

public interface IBehaviorBuilder<TSaga, TMessage>
    where TSaga : class, ISagaState, new()
    where TMessage : class, ICorrelatedMessage
{
    IBehaviorBuilder<TSaga, TMessage> Then(Func<BehaviorContext<TSaga, TMessage>, Task> action);
    IBehaviorBuilder<TSaga, TMessage> Then(Action<BehaviorContext<TSaga, TMessage>> action);
    IStateConfigurator<TSaga> TransitionTo(State state);
    IStateConfigurator<TSaga> Finalize();
}

// ── implementações internas ──────────────────────────────────────────────────

internal sealed class StateConfigurator<TSaga> : IStateConfigurator<TSaga>
    where TSaga : class, ISagaState, new()
{
    private readonly string[] _stateNames;
    private readonly Dictionary<(string state, Type eventType), TransitionDefinition<TSaga>> _transitions;

    public StateConfigurator(
        string stateName,
        Dictionary<(string, Type), TransitionDefinition<TSaga>> transitions)
        : this(transitions, stateName) { }

    public StateConfigurator(
        Dictionary<(string, Type), TransitionDefinition<TSaga>> transitions,
        params string[] stateNames)
    {
        _transitions = transitions;
        _stateNames = stateNames;
    }

    public IBehaviorBuilder<TSaga, TMessage> When<TMessage>(Event<TMessage> @event)
        where TMessage : class, ICorrelatedMessage
        => new BehaviorBuilder<TSaga, TMessage>(_stateNames, _transitions);
}

internal sealed class BehaviorBuilder<TSaga, TMessage> : IBehaviorBuilder<TSaga, TMessage>
    where TSaga : class, ISagaState, new()
    where TMessage : class, ICorrelatedMessage
{
    private readonly string[] _stateNames;
    private readonly Dictionary<(string state, Type eventType), TransitionDefinition<TSaga>> _transitions;
    private readonly List<TransitionDefinition<TSaga>> _defs;

    public BehaviorBuilder(
        string[] stateNames,
        Dictionary<(string, Type), TransitionDefinition<TSaga>> transitions)
    {
        _stateNames = stateNames;
        _transitions = transitions;
        _defs = new List<TransitionDefinition<TSaga>>();

        // Garante que cada (estado, tipo) tenha uma TransitionDefinition
        foreach (var stateName in _stateNames)
        {
            var key = (stateName, typeof(TMessage));
            if (!_transitions.ContainsKey(key))
                _transitions[key] = new TransitionDefinition<TSaga>();

            _defs.Add(_transitions[key]);
        }
    }

    public IBehaviorBuilder<TSaga, TMessage> Then(Func<BehaviorContext<TSaga, TMessage>, Task> action)
    {
        foreach (var def in _defs)
            def.AddAction(action);
        return this;
    }

    public IBehaviorBuilder<TSaga, TMessage> Then(Action<BehaviorContext<TSaga, TMessage>> action)
        => Then(ctx => { action(ctx); return Task.CompletedTask; });

    public IStateConfigurator<TSaga> TransitionTo(State state)
    {
        foreach (var def in _defs)
            def.SetTargetState(state.Name);
        return new StateConfigurator<TSaga>(_transitions, _stateNames);
    }

    public IStateConfigurator<TSaga> Finalize()
        => TransitionTo(new State("Final"));
}