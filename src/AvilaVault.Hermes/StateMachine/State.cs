namespace AvilaVault.Hermes.StateMachine;

public sealed class State
{
    public string Name { get; }

    public State(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("State name cannot be empty.", nameof(name));

        Name = name;
    }

    public override string ToString() => Name;
}