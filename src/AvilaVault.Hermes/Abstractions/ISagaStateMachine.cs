namespace AvilaVault.Hermes.Abstractions
{
    public interface ISagaStateMachine
    {
        IEnumerable<Type> GetRegisteredEventTypes();
        string Name { get; }
    }
}
