namespace AvilaVault.Hermes.Abstractions;

public interface ISagaState
{
    Guid CorrelationId { get; set; }
    string CurrentState { get; set; }
}