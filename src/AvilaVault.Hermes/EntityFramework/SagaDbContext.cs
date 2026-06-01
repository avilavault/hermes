using AvilaVault.Hermes.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AvilaVault.Hermes.EntityFramework;

/// <summary>
/// DbContext base para persistência de sagas via Entity Framework Core.
/// Pode ser estendido pelo usuário para adicionar múltiplas sagas no mesmo contexto.
/// </summary>
public class SagaDbContext : DbContext, ISagaDbContext
{
    public SagaDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SagaDbContext).Assembly);
    }
}

/// <summary>
/// DbContext genérico para uma saga específica — usado quando não há DbContext customizado.
/// </summary>
/// <typeparam name="TSaga">Tipo do estado da saga</typeparam>
public class SagaDbContext<TSaga> : SagaDbContext
    where TSaga : class, ISagaState
{
    public SagaDbContext(DbContextOptions<SagaDbContext<TSaga>> options) : base(options) { }

    public DbSet<TSaga> SagaStates => Set<TSaga>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TSaga>(entity =>
        {
            var tableName = typeof(TSaga).Name
                .Replace("SagaState", "")
                .Replace("State", "")
                .Replace("Saga", "");

            entity.ToTable($"{tableName}State");

            entity.HasKey(e => e.CorrelationId);

            entity.Property(e => e.CurrentState)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(e => e.CurrentState);

            entity.Property<byte[]>("RowVersion")
                .IsRowVersion()
                .HasColumnName("RowVersion");
        });
    }
}