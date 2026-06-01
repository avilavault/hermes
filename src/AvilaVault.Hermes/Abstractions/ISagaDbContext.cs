using Microsoft.EntityFrameworkCore;

namespace AvilaVault.Hermes.Abstractions;

/// <summary>
/// Contrato para DbContext de sagas — permite IoC e mocking.
/// </summary>
public interface ISagaDbContext : IDisposable, IAsyncDisposable
{
    DbSet<TSaga> Set<TSaga>() where TSaga : class;

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Retorna a entrada do estado da entidade para controle de concorrência.
    /// </summary>
    Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TSaga> Entry<TSaga>(TSaga entity)
        where TSaga : class;
}