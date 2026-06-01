using AvilaVault.Hermes.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvilaVault.Hermes.EntityFramework;

/// <summary>
/// Repositório de sagas com persistência via Entity Framework Core.
/// Implementa controle de concorrência otimista com RowVersion.
/// </summary>
internal sealed class EntityFrameworkSagaRepository<TSaga>(
        ISagaDbContext dbContext,
        ILogger<EntityFrameworkSagaRepository<TSaga>> logger) : ISagaRepository<TSaga>
    where TSaga : class, ISagaState, new()
{
    private readonly ISagaDbContext _dbContext = dbContext;
    private readonly ILogger<EntityFrameworkSagaRepository<TSaga>> _logger = logger;

    public async Task<TSaga?> GetAsync(Guid correlationId, CancellationToken ct = default)
    {
        try
        {
            var saga = await _dbContext.Set<TSaga>()
                .FirstOrDefaultAsync(s => s.CorrelationId == correlationId, ct);

            return saga;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "[Hermes.EF] Erro ao carregar saga {SagaType} com CorrelationId={CorrelationId}",
                typeof(TSaga).Name, correlationId);
            throw;
        }
    }

    public async Task SaveAsync(TSaga saga, CancellationToken ct = default)
    {
        const int maxRetries = 3;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                var entry = _dbContext.Entry(saga);

                if (entry.State == EntityState.Detached)
                    _dbContext.Set<TSaga>().Add(saga);
                else if (entry.State == EntityState.Unchanged)
                    entry.State = EntityState.Modified;

                await _dbContext.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                attempt++;
                _logger.LogWarning(
                    "[Hermes.EF] Conflito de concorrência ao salvar saga {SagaType} (tentativa {Attempt}/{Max}). CorrelationId={CorrelationId}",
                    typeof(TSaga).Name, attempt, maxRetries, saga.CorrelationId);

                if (attempt >= maxRetries)
                {
                    _logger.LogError(ex,
                        "[Hermes.EF] Falha permanente ao salvar saga {SagaType} após {Retries} tentativas. CorrelationId={CorrelationId}",
                        typeof(TSaga).Name, maxRetries, saga.CorrelationId);
                    throw;
                }

                foreach (var entity in ex.Entries)
                    await entity.ReloadAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Hermes.EF] Erro inesperado ao salvar saga {SagaType}. CorrelationId={CorrelationId}",
                    typeof(TSaga).Name, saga.CorrelationId);
                throw;
            }
        }
    }

    public async Task DeleteAsync(Guid correlationId, CancellationToken ct = default)
    {
        try
        {
            var saga = await _dbContext.Set<TSaga>()
                .FirstOrDefaultAsync(s => s.CorrelationId == correlationId, ct);

            if (saga != null)
            {
                _dbContext.Set<TSaga>().Remove(saga);
                await _dbContext.SaveChangesAsync(ct);

                _logger.LogDebug(
                    "[Hermes.EF] Saga {SagaType} deletada. CorrelationId={CorrelationId}",
                    typeof(TSaga).Name, correlationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Hermes.EF] Erro ao deletar saga {SagaType}. CorrelationId={CorrelationId}",
                typeof(TSaga).Name, correlationId);
            throw;
        }
    }
}