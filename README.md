# Hermes

<p align="center">
  <img width="350" src="./docs/LABEL_WITH_BACKGROUND.svg" /><br/>
  <a href="https://github.com/avilavault/hermes/actions/workflows/ci-cd.yml">
    <img src="https://github.com/avilavault/hermes/actions/workflows/ci-cd.yml/badge.svg" />
  </a>
  <a href="https://www.nuget.org/packages/AvilaVault.Hermes/">
    <img src="https://img.shields.io/nuget/v/AvilaVault.Hermes.svg" />
  </a>
  <a href="https://www.nuget.org/packages/AvilaVault.Hermes/">
    <img src="https://img.shields.io/nuget/dt/AvilaVault.Hermes.svg" />
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/license-Proprietary-blue.svg" />
  </a>
</p>

**Saga State Machine framework for .NET 10 — orchestrate long-running distributed processes**

Hermes manages complex business processes through persistent state machines, ensuring eventual consistency in distributed architectures. Inspired by MassTransit Saga/Automatonymous, designed to be simple, explicit, and dependency-free.

---

## 🎯 Use Cases

Use Hermes when you need to coordinate multiple async operations that can fail independently:

✅ **Checkout processes** — Order → Payment → Fulfillment → Delivery  
✅ **Customer onboarding** — Registration → Verification → Approval → Activation  
✅ **Approval workflows** — Request → Review → Approval → Execution  
✅ **Microservice orchestration** — Coordinate operations across multiple services  
✅ **Transaction compensation** — Automatic rollback when a step fails

---

## 🚀 Quick Start

### 1. Installation

```bash
dotnet add package AvilaVault.Hermes
```

### 2. Define your messages

Correlated messages represent events in your process:

```csharp
using AvilaVault.Hermes.Abstractions;

public record OrderPlaced(Guid CorrelationId, string OrderNumber, decimal Amount) 
    : ICorrelatedMessage;

public record PaymentApproved(Guid CorrelationId) 
    : ICorrelatedMessage;

public record PaymentRejected(Guid CorrelationId, string Reason) 
    : ICorrelatedMessage;

public record OrderDelivered(Guid CorrelationId) 
    : ICorrelatedMessage;
```

### 3. Create the saga state

State persists between events:

```csharp
using AvilaVault.Hermes.Abstractions;

public class OrderSagaState : ISagaState
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = "Initial";
    
    // Process data
    public string? OrderNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public bool WasCancelled { get; set; }
}
```

### 4. Implement the state machine

Fluent DSL to define transitions:

```csharp
using AvilaVault.Hermes.StateMachine;

public class OrderStateMachine : HermesStateMachine<OrderSagaState>
{
    // Define custom states
    public State Submitted { get; } = new("Submitted");
    public State Paid { get; } = new("Paid");
    public State Cancelled { get; } = new("Cancelled");

    // Define events
    public Event<OrderPlaced> OrderPlaced { get; } = new();
    public Event<PaymentApproved> PaymentApproved { get; } = new();
    public Event<PaymentRejected> PaymentRejected { get; } = new();
    public Event<OrderDelivered> OrderDelivered { get; } = new();

    public OrderStateMachine()
    {
        // Initial state
        Initially()
            .When(OrderPlaced)
                .Then(ctx =>
                {
                    ctx.Saga.OrderNumber = ctx.Message.OrderNumber;
                    ctx.Saga.TotalAmount = ctx.Message.Amount;
                })
                .TransitionTo(Submitted);

        // During "Submitted" state
        During(Submitted)
            .When(PaymentApproved)
                .TransitionTo(Paid)
            .When(PaymentRejected)
                .Then(ctx => ctx.Saga.WasCancelled = true)
                .TransitionTo(Cancelled);

        // During "Paid" state
        During(Paid)
            .When(OrderDelivered)
                .Then(ctx => 
                    Console.WriteLine($"Order {ctx.Saga.OrderNumber} delivered!"))
                .Finalize(); // Mark saga as completed
    }
}
```

### 5. Register in DI

```csharp
using AvilaVault.Hermes.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHermes(cfg =>
{
    cfg.AddSagaStateMachine<OrderStateMachine, OrderSagaState>()
       .InMemory(); // or .UseSqlServer() / .UseRedis() in production
});

var app = builder.Build();
```

### 6. Publish events

```csharp
using AvilaVault.Hermes.Abstractions;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IHermesBus _bus;

    public OrdersController(IHermesBus bus) => _bus = bus;

    [HttpPost]
    public async Task<IActionResult> PlaceOrder(PlaceOrderRequest request)
    {
        var correlationId = Guid.NewGuid();
        
        await _bus.PublishAsync(new OrderPlaced(
            correlationId, 
            request.OrderNumber, 
            request.Amount));

        return Accepted(new { correlationId });
    }

    [HttpPost("{correlationId}/approve-payment")]
    public async Task<IActionResult> ApprovePayment(Guid correlationId)
    {
        await _bus.PublishAsync(new PaymentApproved(correlationId));
        return Ok();
    }
}
```

---

## 📐 Core Concepts

### CorrelationId

Each saga is identified by a unique `CorrelationId` — the same ID flows through all messages in the process. Hermes uses this ID to locate and update the correct state.

### States

- **Initial** — default state for every new saga
- **Final** — terminal state (saga completed)
- **Custom states** — you define according to your process

### Events

Events trigger transitions. Use `Event<TMessage>` to declare typed events.

### Transitions

Fluent DSL to define behavior:

```csharp
Initially()           // In initial state
    .When(EventName)  // When this event arrives
        .Then(ctx => { /* logic */ })  // Execute this
        .TransitionTo(NewState);        // And change to this state

During(SomeState)     // During this state
    .When(EventName)
        .Finalize();  // Mark as completed (goes to "Final")
```

### Behaviors

Chain multiple `.Then()` to execute side-effects:

```csharp
During(Paid)
    .When(OrderDelivered)
        .Then(ctx => _logger.LogInformation("Delivery confirmed"))
        .Then(ctx => _emailService.SendReceipt(ctx.Saga.OrderNumber))
        .Then(ctx => _analytics.Track("OrderCompleted"))
        .Finalize();
```

---

## 🔄 Execution Flow

1. Message arrives via `IHermesBus.PublishAsync()`
2. Hermes retrieves saga by `CorrelationId` (or creates a new one if it doesn't exist)
3. State machine looks for a transition for `(CurrentState, TMessage)`
4. If found, executes behaviors and updates state
5. Persists new state in `ISagaRepository`
6. If event is not expected in that state, it's silently ignored

---

## 💾 Persistence

### In-Memory (development/testing)

```csharp
cfg.AddSagaStateMachine<MyStateMachine, MyState>()
   .InMemory();
```

Uses `ConcurrentDictionary` — doesn't survive restarts.

### Production (implement)

You can implement `ISagaRepository<TSaga>` to persist in:

- SQL Server / PostgreSQL / MySQL
- Redis
- MongoDB
- CosmosDB
- Any store that supports lookup by CorrelationId

```csharp
public interface ISagaRepository<TSaga>
    where TSaga : class, ISagaState, new()
{
    Task<TSaga?> GetAsync(Guid correlationId, CancellationToken ct = default);
    Task SaveAsync(TSaga saga, CancellationToken ct = default);
}
```

---

## 🧪 Testing Sagas

```csharp
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class OrderSagaTests
{
    private IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
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
}
```

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────┐
│                 Your Application                     │
│  (Controllers, Workers, Consumers, etc.)             │
└────────────────────┬────────────────────────────────┘
                     │ PublishAsync<TMessage>
                     ▼
┌─────────────────────────────────────────────────────┐
│                  IHermesBus                          │
│  Routes messages to registered handlers              │
└────────────────────┬────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────┐
│          ISagaMessageHandler<TMessage>               │
│  1. GetAsync(correlationId) from repository          │
│  2. Dispatch to state machine                        │
│  3. SaveAsync(saga) after transition                 │
└────────────────────┬────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────┐
│         HermesStateMachine<TSaga>                    │
│  Finds transition (CurrentState, TMessage)           │
│  Executes behaviors                                  │
│  Updates CurrentState                                │
└─────────────────────────────────────────────────────┘
```

---

## 📦 Project Structure

```
AvilaVault.Hermes/
├── Abstractions/
│   ├── ICorrelatedMessage.cs       # Message contract
│   ├── IHermesBus.cs                # Event bus
│   ├── ISagaState.cs                # State contract
│   ├── ISagaRepository.cs           # Persistence
│   └── IEventContext.cs             # Execution context
├── StateMachine/
│   ├── HermesStateMachine.cs        # Base class
│   ├── State.cs                     # State representation
│   ├── Event.cs                     # Event representation
│   ├── BehaviorBuilder.cs           # Fluent DSL
│   └── TransitionDefinition.cs      # Transition metadata
├── InMemory/
│   ├── InMemoryHermesBus.cs         # In-memory implementation
│   ├── InMemorySagaRepository.cs    # Memory repository
│   └── SagaMessageHandler.cs        # Generic handler
└── DependencyInjection/
    └── HermesServiceCollectionExtensions.cs
```

---

## 🔗 Differences from MassTransit

| Feature                     | MassTransit Saga       | Hermes                |
|-----------------------------|------------------------|-----------------------|
| Transport integration       | RabbitMQ, Azure SB, etc | Transport-agnostic (bring your own) |
| Scheduling                  | Built-in (Quartz)      | Manual implementation |
| State persistence           | Entity Framework       | You implement         |
| Automatic retry             | Yes                    | Delegated to transport |
| Saga correlation            | Via message            | Via `CorrelationId`   |
| Learning curve              | Steep                  | Gentle                |
| Dependencies                | Many                   | Zero (beyond BCL)     |

Hermes is **opinionated and minimalist** — you have full control over transport, persistence, and retry policies.

---

## 🧪 Running Tests

```bash
dotnet test
```

### With Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## 📄 License

Property of **AvilaVault**. All rights reserved.

---

## 🙏 Acknowledgments

Inspired by [MassTransit](https://github.com/MassTransit/MassTransit), [NServiceBus Saga](https://docs.particular.net/nservicebus/sagas/), and distributed orchestration patterns from the .NET community.
