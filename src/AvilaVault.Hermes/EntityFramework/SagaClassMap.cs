using AvilaVault.Hermes.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvilaVault.Hermes.EntityFramework;

/// <summary>
/// Configuração base para mapeamento de sagas no EF Core.
/// Derive desta classe para customizar o mapeamento de sagas específicas.
/// </summary>
/// <typeparam name="TSaga">Tipo do estado da saga</typeparam>
public class SagaClassMap<TSaga> : IEntityTypeConfiguration<TSaga>
    where TSaga : class, ISagaState
{
    public virtual void Configure(EntityTypeBuilder<TSaga> builder)
    {
        var tableName = GetTableName(typeof(TSaga));
        builder.ToTable(tableName);

        builder.HasKey(e => e.CorrelationId);

        builder.Property(e => e.CurrentState)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(e => e.CurrentState);

        builder.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .HasColumnName("RowVersion");

        ConfigureProperties(builder);
    }

    /// <summary>
    /// Sobrescreva este método para adicionar configurações específicas da saga.
    /// </summary>
    protected virtual void ConfigureProperties(EntityTypeBuilder<TSaga> builder) { }

    /// <summary>
    /// Estratégia de nomenclatura de tabelas — pode ser sobrescrita.
    /// </summary>
    protected virtual string GetTableName(Type sagaType)
    {
        var name = sagaType.Name
            .Replace("SagaState", "")
            .Replace("State", "")
            .Replace("Saga", "");

        return $"{name}State";
    }
}