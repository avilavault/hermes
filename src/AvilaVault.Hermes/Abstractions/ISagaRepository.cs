namespace AvilaVault.Hermes.Abstractions;

public interface ISagaRepository<TSaga>
    where TSaga : class, ISagaState, new()
{
    Task<TSaga?> GetAsync(Guid correlationId, CancellationToken ct = default);
    Task SaveAsync(TSaga saga, CancellationToken ct = default);
    Task DeleteAsync(Guid correlationId, CancellationToken ct = default);
}