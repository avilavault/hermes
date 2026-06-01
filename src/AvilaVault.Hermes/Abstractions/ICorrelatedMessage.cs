namespace AvilaVault.Hermes.Abstractions;

/// <summary>
/// Contrato que toda mensagem deve implementar para que o Hermes
/// consiga extrair o CorrelationId e localizar a saga correta.
/// </summary>
public interface ICorrelatedMessage
{
    Guid CorrelationId { get; }
}