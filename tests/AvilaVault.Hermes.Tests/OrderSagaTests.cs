using AvilaVault.Hermes.Abstractions;
using AvilaVault.Hermes.DependencyInjection;
using AvilaVault.Hermes.StateMachine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AvilaVault.Hermes.Tests;

// ── Mensagens ────────────────────────────────────────────────────────────────

public record OrderPlaced(Guid CorrelationId, string OrderNumber, decimal Amount)
    : ICorrelatedMessage;

public record PaymentApproved(Guid CorrelationId)
    : ICorrelatedMessage;

public record PaymentRejected(Guid CorrelationId, string Reason)
    : ICorrelatedMessage;

public record OrderDelivered(Guid CorrelationId)
    : ICorrelatedMessage;

// ── Estado ───────────────────────────────────────────────────────────────────

public class OrderSagaState : ISagaState
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = "Initial";
    public string? OrderNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public bool WasCancelled { get; set; }
}

// ── State Machine ─────────────────────────────────────────────────────────────

public class OrderStateMachine : HermesStateMachine<OrderSagaState>
{
    public State Submitted { get; } = new("Submitted");
    public State Paid { get; } = new("Paid");
    public State Cancelled { get; } = new("Cancelled");

    public Event<OrderPlaced> OrderPlaced { get; } = new();
    public Event<PaymentApproved> PaymentApproved { get; } = new();
    public Event<PaymentRejected> PaymentRejected { get; } = new();
    public Event<OrderDelivered> OrderDelivered { get; } = new();

    public OrderStateMachine()
    {
        Initially()
            .When(OrderPlaced)
                .Then(ctx =>
                {
                    ctx.Saga.OrderNumber = ctx.Message.OrderNumber;
                    ctx.Saga.TotalAmount = ctx.Message.Amount;
                })
                .TransitionTo(Submitted);

        During(Submitted)
            .When(PaymentApproved)
                .TransitionTo(Paid)
            .When(PaymentRejected)
                .Then(ctx => ctx.Saga.WasCancelled = true)
                .TransitionTo(Cancelled);

        During(Paid)
            .When(OrderDelivered)
                .Then(ctx => Console.WriteLine($"[Hermes] ✦ Pedido {ctx.Saga.OrderNumber} entregue. Saga finalizada."))
                .Finalize();
    }
}

// ── Testes ────────────────────────────────────────────────────────────────────

public class OrderSagaTests
{
    private IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddHermes(cfg =>
        {
            cfg.AddSagaStateMachine<OrderStateMachine, OrderSagaState>()
               .InMemory();
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PlacingOrder_ShouldTransition_ToSubmitted()
    {
        var provider = BuildProvider();
        var bus = provider.GetRequiredService<IHermesBus>();
        var repo = provider.GetRequiredService<ISagaRepository<OrderSagaState>>();

        var orderId = Guid.NewGuid();
        await bus.PublishAsync(new OrderPlaced(orderId, "ORD-001", 299.90m));

        var saga = await repo.GetAsync(orderId);

        Assert.NotNull(saga);
        Assert.Equal("Submitted", saga.CurrentState);
        Assert.Equal("ORD-001", saga.OrderNumber);
        Assert.Equal(299.90m, saga.TotalAmount);
    }

    [Fact]
    public async Task ApprovingPayment_ShouldTransition_ToPaid()
    {
        var provider = BuildProvider();
        var bus = provider.GetRequiredService<IHermesBus>();
        var repo = provider.GetRequiredService<ISagaRepository<OrderSagaState>>();

        var orderId = Guid.NewGuid();
        await bus.PublishAsync(new OrderPlaced(orderId, "ORD-002", 150m));
        await bus.PublishAsync(new PaymentApproved(orderId));

        var saga = await repo.GetAsync(orderId);

        Assert.NotNull(saga);
        Assert.Equal("Paid", saga.CurrentState);
    }

    [Fact]
    public async Task RejectingPayment_ShouldTransition_ToCancelled()
    {
        var provider = BuildProvider();
        var bus = provider.GetRequiredService<IHermesBus>();
        var repo = provider.GetRequiredService<ISagaRepository<OrderSagaState>>();

        var orderId = Guid.NewGuid();
        await bus.PublishAsync(new OrderPlaced(orderId, "ORD-003", 500m));
        await bus.PublishAsync(new PaymentRejected(orderId, "Saldo insuficiente"));

        var saga = await repo.GetAsync(orderId);

        Assert.NotNull(saga);
        Assert.Equal("Cancelled", saga.CurrentState);
        Assert.True(saga.WasCancelled);
    }

    [Fact]
    public async Task EventInWrongState_ShouldBeIgnored_Silently()
    {
        var provider = BuildProvider();
        var bus = provider.GetRequiredService<IHermesBus>();
        var repo = provider.GetRequiredService<ISagaRepository<OrderSagaState>>();

        var orderId = Guid.NewGuid();
        await bus.PublishAsync(new OrderPlaced(orderId, "ORD-004", 100m));
        await bus.PublishAsync(new PaymentApproved(orderId));

        // Evento em estado errado — deve ser ignorado silenciosamente
        await bus.PublishAsync(new PaymentRejected(orderId, "Duplicado"));

        var sagaAfterIgnored = await repo.GetAsync(orderId);
        Assert.NotNull(sagaAfterIgnored);
        Assert.Equal("Paid", sagaAfterIgnored.CurrentState);
        Assert.False(sagaAfterIgnored.WasCancelled);

        // Agora finaliza corretamente
        await bus.PublishAsync(new OrderDelivered(orderId));

        var sagaAfterFinal = await repo.GetAsync(orderId);
        Assert.Null(sagaAfterFinal); // removida do repositório ao atingir Final
    }
}