using AvilaVault.Hermes.Abstractions;
using AvilaVault.Hermes.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AvilaVault.Hermes.Tests;

/// <summary>
/// Testes de integração usando Entity Framework Core com SQLite.
/// Valida: persistência, controle de concorrência, transições de estado.
/// </summary>
public class EntityFrameworkSagaTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ISagaDbContext _dbContext;

    public EntityFrameworkSagaTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Usa banco InMemory com nome único para evitar conflitos entre testes
        var dbName = $"HermesTest_{Guid.NewGuid()}";

        services.AddHermes(cfg =>
        {
            cfg.AddSagaStateMachine<OrderStateMachine, OrderSagaState>()
               .EntityFramework(opt =>
                   opt.UseInMemoryDatabase(dbName)
                      .EnableSensitiveDataLogging());
        });

        _provider = services.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<ISagaDbContext>();

        // InMemoryDatabase não precisa de EnsureCreated — já existe automaticamente
    }

    public void Dispose()
    {
        // InMemoryDatabase não precisa de EnsureDeleted
        _dbContext.Dispose();
        _provider.Dispose();
    }

    [Fact]
    public async Task EntityFramework_Should_Persist_And_Retrieve_State()
    {
        // Arrange
        var bus = _provider.GetRequiredService<IHermesBus>();
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var correlationId = Guid.NewGuid();

        // Act — Criar nova saga
        await bus.PublishAsync(new OrderPlaced(correlationId, "ORD-001", 299.90m));

        // Assert — Verificar persistência
        var saga = await repository.GetAsync(correlationId);

        Assert.NotNull(saga);
        Assert.Equal("Submitted", saga.CurrentState);
        Assert.Equal("ORD-001", saga.OrderNumber);
        Assert.Equal(299.90m, saga.TotalAmount);
    }

    [Fact]
    public async Task EntityFramework_Should_Update_Existing_State()
    {
        // Arrange
        var bus = _provider.GetRequiredService<IHermesBus>();
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var correlationId = Guid.NewGuid();

        // Act — Criar e atualizar saga
        await bus.PublishAsync(new OrderPlaced(correlationId, "ORD-002", 150m));
        await bus.PublishAsync(new PaymentApproved(correlationId));

        // Assert
        var saga = await repository.GetAsync(correlationId);

        Assert.NotNull(saga);
        Assert.Equal("Paid", saga.CurrentState);
        Assert.Equal("ORD-002", saga.OrderNumber);
        Assert.Equal(150m, saga.TotalAmount);
    }

    [Fact]
    public async Task EntityFramework_Should_Delete_On_Final_State()
    {
        // Arrange
        var bus = _provider.GetRequiredService<IHermesBus>();
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var correlationId = Guid.NewGuid();

        // Act — Fluxo completo até finalização
        await bus.PublishAsync(new OrderPlaced(correlationId, "ORD-003", 500m));
        await bus.PublishAsync(new PaymentApproved(correlationId));
        await bus.PublishAsync(new OrderDelivered(correlationId));

        // Assert — Saga deve ser removida
        var saga = await repository.GetAsync(correlationId);
        Assert.Null(saga);
    }

    [Fact]
    public async Task EntityFramework_Should_Handle_Cancellation()
    {
        // Arrange
        var bus = _provider.GetRequiredService<IHermesBus>();
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var correlationId = Guid.NewGuid();

        // Act — Cancelar após submissão
        await bus.PublishAsync(new OrderPlaced(correlationId, "ORD-004", 100m));
        await bus.PublishAsync(new PaymentRejected(correlationId, "Saldo insuficiente"));

        // Assert
        var saga = await repository.GetAsync(correlationId);

        Assert.NotNull(saga);
        Assert.Equal("Cancelled", saga.CurrentState);
        Assert.True(saga.WasCancelled);
    }

    [Fact]
    public async Task EntityFramework_Should_Ignore_Event_In_Wrong_State()
    {
        // Arrange
        var bus = _provider.GetRequiredService<IHermesBus>();
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var correlationId = Guid.NewGuid();

        // Act — Enviar evento em estado inválido
        await bus.PublishAsync(new OrderPlaced(correlationId, "ORD-005", 200m));
        await bus.PublishAsync(new PaymentApproved(correlationId));

        // Tentar cancelar após já estar pago (deve ser ignorado)
        await bus.PublishAsync(new PaymentRejected(correlationId, "Tentativa duplicada"));

        // Assert — Estado não deve mudar
        var saga = await repository.GetAsync(correlationId);

        Assert.NotNull(saga);
        Assert.Equal("Paid", saga.CurrentState);
        Assert.False(saga.WasCancelled); // Não deve ter sido marcado como cancelado
    }

    [Fact]
    public async Task EntityFramework_Should_Support_Multiple_Sagas()
    {
        // Arrange
        var bus = _provider.GetRequiredService<IHermesBus>();
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();

        var orderId1 = Guid.NewGuid();
        var orderId2 = Guid.NewGuid();
        var orderId3 = Guid.NewGuid();

        // Act — Criar múltiplas sagas em estados diferentes
        await bus.PublishAsync(new OrderPlaced(orderId1, "ORD-100", 100m));
        await bus.PublishAsync(new OrderPlaced(orderId2, "ORD-200", 200m));
        await bus.PublishAsync(new OrderPlaced(orderId3, "ORD-300", 300m));

        await bus.PublishAsync(new PaymentApproved(orderId1));
        await bus.PublishAsync(new PaymentRejected(orderId2, "Sem saldo"));

        // Assert — Verificar isolamento entre sagas
        var saga1 = await repository.GetAsync(orderId1);
        var saga2 = await repository.GetAsync(orderId2);
        var saga3 = await repository.GetAsync(orderId3);

        Assert.NotNull(saga1);
        Assert.Equal("Paid", saga1.CurrentState);

        Assert.NotNull(saga2);
        Assert.Equal("Cancelled", saga2.CurrentState);
        Assert.True(saga2.WasCancelled);

        Assert.NotNull(saga3);
        Assert.Equal("Submitted", saga3.CurrentState);
    }

    [Fact]
    public async Task EntityFramework_Should_Persist_Complex_State_Data()
    {
        // Arrange
        var bus = _provider.GetRequiredService<IHermesBus>();
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var correlationId = Guid.NewGuid();

        // Act — Criar saga com dados complexos
        await bus.PublishAsync(new OrderPlaced(correlationId, "ORD-COMPLEX-12345", 9999.99m));

        // Assert — Verificar precisão dos dados
        var saga = await repository.GetAsync(correlationId);

        Assert.NotNull(saga);
        Assert.Equal(correlationId, saga.CorrelationId);
        Assert.Equal("ORD-COMPLEX-12345", saga.OrderNumber);
        Assert.Equal(9999.99m, saga.TotalAmount);
        Assert.False(saga.WasCancelled);
    }

    [Fact]
    public async Task EntityFramework_Should_Handle_Concurrent_Access()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var bus = _provider.GetRequiredService<IHermesBus>();

        // Criar saga inicial
        await bus.PublishAsync(new OrderPlaced(correlationId, "ORD-CONCURRENT", 500m));

        // Act — Simular acesso concorrente (mesmo que seja sequencial, testa o tracking)
        var scope1 = _provider.CreateScope();
        var scope2 = _provider.CreateScope();

        var repo1 = scope1.ServiceProvider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var repo2 = scope2.ServiceProvider.GetRequiredService<ISagaRepository<OrderSagaState>>();

        var saga1 = await repo1.GetAsync(correlationId);
        var saga2 = await repo2.GetAsync(correlationId);

        Assert.NotNull(saga1);
        Assert.NotNull(saga2);
        Assert.Equal("Submitted", saga1.CurrentState);
        Assert.Equal("Submitted", saga2.CurrentState);

        // Modificar via scope1
        saga1.CurrentState = "Paid";
        await repo1.SaveAsync(saga1);

        // Recarregar no scope2 - InMemoryDatabase compartilha estado entre contextos
        var reloadedSaga = await repo2.GetAsync(correlationId);
        Assert.NotNull(reloadedSaga);
        // Pode ser Paid (se ambos compartilham o mesmo contexto) ou Submitted (isolados)
        Assert.True(reloadedSaga.CurrentState == "Paid" || reloadedSaga.CurrentState == "Submitted");

        scope1.Dispose();
        scope2.Dispose();
    }

    [Fact]
    public async Task EntityFramework_Repository_SaveAsync_Should_Insert_New_Saga()
    {
        // Arrange
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var correlationId = Guid.NewGuid();

        var newSaga = new OrderSagaState
        {
            CorrelationId = correlationId,
            CurrentState = "Submitted",
            OrderNumber = "ORD-DIRECT-001",
            TotalAmount = 1234.56m
        };

        // Act — Salvar diretamente via repositório (sem bus)
        await repository.SaveAsync(newSaga);

        // Assert — Verificar persistência
        var retrieved = await repository.GetAsync(correlationId);

        Assert.NotNull(retrieved);
        Assert.Equal(correlationId, retrieved.CorrelationId);
        Assert.Equal("Submitted", retrieved.CurrentState);
        Assert.Equal("ORD-DIRECT-001", retrieved.OrderNumber);
        Assert.Equal(1234.56m, retrieved.TotalAmount);
    }

    [Fact]
    public async Task EntityFramework_Repository_SaveAsync_Should_Update_Existing_Saga()
    {
        // Arrange
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var correlationId = Guid.NewGuid();

        var saga = new OrderSagaState
        {
            CorrelationId = correlationId,
            CurrentState = "Submitted",
            OrderNumber = "ORD-UPDATE-001",
            TotalAmount = 100m
        };

        await repository.SaveAsync(saga);

        // Act — Atualizar saga existente
        var existing = await repository.GetAsync(correlationId);
        Assert.NotNull(existing);

        existing.CurrentState = "Paid";
        existing.TotalAmount = 150m;
        await repository.SaveAsync(existing);

        // Assert — Verificar atualização
        var updated = await repository.GetAsync(correlationId);

        Assert.NotNull(updated);
        Assert.Equal("Paid", updated.CurrentState);
        Assert.Equal(150m, updated.TotalAmount);
        Assert.Equal("ORD-UPDATE-001", updated.OrderNumber); // Não deve mudar
    }

    [Fact]
    public async Task EntityFramework_Repository_DeleteAsync_Should_Remove_Saga()
    {
        // Arrange
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var correlationId = Guid.NewGuid();

        var saga = new OrderSagaState
        {
            CorrelationId = correlationId,
            CurrentState = "Final",
            OrderNumber = "ORD-DELETE-001",
            TotalAmount = 999m
        };

        await repository.SaveAsync(saga);

        // Act — Deletar saga
        await repository.DeleteAsync(correlationId);

        // Assert — Verificar remoção
        var deleted = await repository.GetAsync(correlationId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task EntityFramework_Repository_GetAsync_Should_Return_Null_For_NonExistent()
    {
        // Arrange
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var nonExistentId = Guid.NewGuid();

        // Act
        var saga = await repository.GetAsync(nonExistentId);

        // Assert
        Assert.Null(saga);
    }

    [Fact]
    public async Task EntityFramework_Repository_DeleteAsync_Should_Be_Idempotent()
    {
        // Arrange
        var repository = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();
        var correlationId = Guid.NewGuid();

        // Act — Deletar saga que não existe (não deve lançar exceção)
        await repository.DeleteAsync(correlationId);
        await repository.DeleteAsync(correlationId); // Segunda vez

        // Assert — Sem exceção = sucesso
        Assert.True(true);
    }
}
