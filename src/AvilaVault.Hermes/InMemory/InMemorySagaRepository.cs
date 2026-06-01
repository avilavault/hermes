using System.Collections.Concurrent;
using AvilaVault.Hermes.Abstractions;

namespace AvilaVault.Hermes.InMemory;

internal sealed class InMemorySagaRepository<TSaga> : ISagaRepository<TSaga>
    where TSaga : class, ISagaState, new()
{
    private readonly ConcurrentDictionary<Guid, TSaga> _store = new();

    public Task<TSaga?> GetAsync(Guid correlationId, CancellationToken ct = default)
    {
        _store.TryGetValue(correlationId, out var saga);
        return Task.FromResult(saga);
    }

    public Task SaveAsync(TSaga saga, CancellationToken ct = default)
    {
        _store[saga.CorrelationId] = saga;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid correlationId, CancellationToken ct = default)
    {
        _store.TryRemove(correlationId, out _);
        return Task.CompletedTask;
    }
}