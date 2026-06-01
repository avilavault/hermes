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

## ✨ Features

🎯 **Type-safe fluent DSL** — compile-time guarantees for state transitions  
💾 **Multiple persistence options** — in-memory, Entity Framework Core, or custom  
🔒 **Optimistic concurrency control** — automatic retry on conflicts (EF Core)  
🧪 **Test-friendly** — built-in in-memory provider for unit tests  
📊 **Query support** — direct EF Core queries on saga state  
🚀 **Production-ready** — scoped lifetimes, comprehensive logging, error handling  
🔌 **Transport-agnostic** — works with any message bus or queue  
📦 **Zero dependencies** — only Microsoft.Extensions.* for DI and logging  
🏗️ **Convention-based** — automatic table naming and indexing  
⚡ **High-performance** — minimal allocations, efficient state lookups

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
       .InMemory(); // for development/testing
       // .EntityFramework(opt => opt.UseSqlServer(connectionString)); // for production
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

## 🏭 Production Setup with Entity Framework Core

### 1. Install EF Core Provider

```bash
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
# or
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
# or
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

### 2. Configure Persistence

```csharp
using AvilaVault.Hermes.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHermes(cfg =>
{
    cfg.AddSagaStateMachine<OrderStateMachine, OrderSagaState>()
       .EntityFramework(opt => 
           opt.UseSqlServer(
               builder.Configuration.GetConnectionString("DefaultConnection"),
               sqlOpt => sqlOpt.MigrationsAssembly("MyApp")));
});

var app = builder.Build();
```

### 3. Generate Database Migrations

```bash
# Install EF Core tools
dotnet tool install --global dotnet-ef

# Generate migration
dotnet ef migrations add InitialSagaCreate

# Apply to database
dotnet ef database update
```

This creates a table with the following schema:

```sql
CREATE TABLE OrderState (
    CorrelationId    UNIQUEIDENTIFIER PRIMARY KEY,
    CurrentState     NVARCHAR(200) NOT NULL,
    OrderNumber      NVARCHAR(MAX) NULL,
    TotalAmount      DECIMAL(18,2) NOT NULL,
    WasCancelled     BIT NOT NULL,
    RowVersion       ROWVERSION NOT NULL
);

CREATE INDEX IX_OrderState_CurrentState ON OrderState(CurrentState);
```

### 4. Handle Concurrency

The repository automatically retries up to 3 times on conflicts:

```csharp
// Two concurrent requests updating the same saga
await Task.WhenAll(
    bus.PublishAsync(new UpdateOrder(correlationId, amount: 100)),
    bus.PublishAsync(new UpdateOrder(correlationId, amount: 200))
);
// ✅ Both succeed — automatic retry handles conflicts
```

### 5. Monitor Saga State

Query saga state directly via Entity Framework:

```csharp
public class OrderQueryService
{
    private readonly ISagaDbContext _dbContext;
    
    public async Task<IEnumerable<OrderSagaState>> GetPendingOrdersAsync()
    {
        return await _dbContext.Set<OrderSagaState>()
            .Where(s => s.CurrentState == "Submitted")
            .ToListAsync();
    }
    
    public async Task<OrderSagaState?> GetOrderAsync(Guid correlationId)
    {
        return await _dbContext.Set<OrderSagaState>()
            .FirstOrDefaultAsync(s => s.CorrelationId == correlationId);
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

### Saga Deletion

Sagas can be explicitly deleted before reaching the Final state:

```csharp
// In your state machine
During(Cancelled)
    .When(OrderExpired)
        .Then(async ctx => 
        {
            var repo = ctx.GetService<ISagaRepository<OrderSagaState>>();
            await repo.DeleteAsync(ctx.Saga.CorrelationId);
        });

// Or via repository directly
var repo = serviceProvider.GetRequiredService<ISagaRepository<OrderSagaState>>();
await repo.DeleteAsync(correlationId);
```

When a saga reaches the `Final` state via `.Finalize()`, it remains in storage but is no longer processed. Use explicit deletion for cleanup scenarios like cancellations or expirations.

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

Hermes supports multiple persistence strategies for saga state storage.

### In-Memory (development/testing)

```csharp
cfg.AddSagaStateMachine<MyStateMachine, MyState>()
   .InMemory();
```

Uses `ConcurrentDictionary` — doesn't survive restarts. Ideal for unit tests and local development.

### Entity Framework Core (production-ready)

Full-featured persistence with automatic optimistic concurrency control.

#### Quick Setup (automatic DbContext)

```csharp
cfg.AddSagaStateMachine<OrderStateMachine, OrderSagaState>()
   .EntityFramework(opt => opt.UseSqlServer(connectionString));
```

Hermes automatically creates a `SagaDbContext<TSaga>` with:
- **CorrelationId** as primary key
- **CurrentState** indexed for efficient queries
- **RowVersion** for optimistic concurrency control
- Automatic table naming (e.g., `OrderSagaState` → `OrderState`)

#### Advanced Setup (custom DbContext)

For multi-saga or shared DbContext scenarios:

```csharp
public class MyAppDbContext : DbContext, ISagaDbContext
{
    public DbSet<OrderSagaState> Orders => Set<OrderSagaState>();
    public DbSet<PaymentSagaState> Payments => Set<PaymentSagaState>();
    
    public MyAppDbContext(DbContextOptions<MyAppDbContext> options) 
        : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Customize mappings
        modelBuilder.ApplyConfiguration(new OrderSagaStateMap());
        modelBuilder.ApplyConfiguration(new PaymentSagaStateMap());
    }
}

// Custom mapping
public class OrderSagaStateMap : SagaClassMap<OrderSagaState>
{
    public override void Configure(EntityTypeBuilder<OrderSagaState> builder)
    {
        base.Configure(builder); // Apply default conventions
        
        builder.ToTable("Orders"); // Custom table name
        builder.Property(x => x.OrderNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.OrderNumber).IsUnique();
    }
}

// Registration
services.AddDbContext<MyAppDbContext>(opt => 
    opt.UseSqlServer(connectionString));

services.AddHermes(cfg =>
{
    cfg.AddSagaStateMachine<OrderStateMachine, OrderSagaState>()
       .EntityFramework<MyAppDbContext>();
       
    cfg.AddSagaStateMachine<PaymentStateMachine, PaymentSagaState>()
       .EntityFramework<MyAppDbContext>();
});
```

#### Supported Providers

- **SQL Server** — `Microsoft.EntityFrameworkCore.SqlServer`
- **PostgreSQL** — `Npgsql.EntityFrameworkCore.PostgreSQL`
- **SQLite** — `Microsoft.EntityFrameworkCore.Sqlite`
- **MySQL** — `Pomelo.EntityFrameworkCore.MySql`
- **In-Memory** — `Microsoft.EntityFrameworkCore.InMemory` (testing only)

#### Database Migrations

Generate and apply migrations using EF Core tools:

```bash
# Install tools
dotnet tool install --global dotnet-ef

# Add migration
dotnet ef migrations add InitialSagaCreate --context SagaDbContext

# Apply to database
dotnet ef database update --context SagaDbContext
```

#### Concurrency Control

Hermes automatically handles optimistic concurrency conflicts:

```csharp
// Automatic retry up to 3 attempts on DbUpdateConcurrencyException
await bus.PublishAsync(new PaymentApproved(correlationId));
```

If concurrent updates occur:
1. First update succeeds
2. Second update detects conflict via RowVersion
3. Entity reloads from database
4. Operation retries automatically (up to 3 attempts)
5. If all retries fail, throws `DbUpdateConcurrencyException`

### Custom Persistence

Implement `ISagaRepository<TSaga>` for other storage systems:

```csharp
public interface ISagaRepository<TSaga>
    where TSaga : class, ISagaState, new()
{
    Task<TSaga?> GetAsync(Guid correlationId, CancellationToken ct = default);
    Task SaveAsync(TSaga saga, CancellationToken ct = default);
    Task DeleteAsync(Guid correlationId, CancellationToken ct = default);
}
```

Supported scenarios:
- **Redis** — fast lookups with JSON serialization
- **MongoDB** — document-based storage
- **CosmosDB** — globally distributed sagas
- **DynamoDB** — AWS-native persistence
- Any store supporting key-value lookups by `CorrelationId`

---

## 🧪 Testing Sagas

### Unit Tests (In-Memory)

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

### Integration Tests (Entity Framework Core)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class OrderSagaIntegrationTests : IAsyncLifetime
{
    private IServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        
        services.AddHermes(cfg =>
        {
            cfg.AddSagaStateMachine<OrderStateMachine, OrderSagaState>()
               .EntityFramework(opt => 
                   opt.UseSqlite("Data Source=:memory:"));
        });

        _provider = services.BuildServiceProvider();
        
        // Create database
        var dbContext = _provider.GetRequiredService<ISagaDbContext>();
        await ((DbContext)dbContext).Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        var dbContext = _provider.GetRequiredService<ISagaDbContext>();
        await ((DbContext)dbContext).Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task CompleteSagaLifecycle_ShouldPersist_AllTransitions()
    {
        var bus = _provider.GetRequiredService<IHermesBus>();
        var repo = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();

        var orderId = Guid.NewGuid();

        // Initial → Submitted
        await bus.PublishAsync(new OrderPlaced(orderId, "ORD-001", 500m));
        var saga = await repo.GetAsync(orderId);
        Assert.Equal("Submitted", saga!.CurrentState);

        // Submitted → Paid
        await bus.PublishAsync(new PaymentApproved(orderId));
        saga = await repo.GetAsync(orderId);
        Assert.Equal("Paid", saga!.CurrentState);

        // Paid → Final
        await bus.PublishAsync(new OrderDelivered(orderId));
        saga = await repo.GetAsync(orderId);
        Assert.Equal("Final", saga!.CurrentState);
    }

    [Fact]
    public async Task ConcurrentUpdates_ShouldHandle_ConflictsGracefully()
    {
        var bus = _provider.GetRequiredService<IHermesBus>();
        var repo = _provider.GetRequiredService<ISagaRepository<OrderSagaState>>();

        var orderId = Guid.NewGuid();
        await bus.PublishAsync(new OrderPlaced(orderId, "ORD-002", 100m));

        // Simulate concurrent updates
        var tasks = Enumerable.Range(1, 10)
            .Select(i => bus.PublishAsync(new UpdateOrderAmount(orderId, i * 10m)))
            .ToArray();

        // All should succeed due to automatic retry
        await Task.WhenAll(tasks);

        var saga = await repo.GetAsync(orderId);
        Assert.NotNull(saga);
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
│   ├── ISagaRepository.cs           # Persistence abstraction
│   ├── ISagaDbContext.cs            # DbContext abstraction
│   └── IEventContext.cs             # Execution context
├── StateMachine/
│   ├── HermesStateMachine.cs        # Base class
│   ├── State.cs                     # State representation
│   ├── Event.cs                     # Event representation
│   ├── BehaviorBuilder.cs           # Fluent DSL
│   └── TransitionDefinition.cs      # Transition metadata
├── InMemory/
│   ├── InMemoryHermesBus.cs         # In-memory event bus
│   ├── InMemorySagaRepository.cs    # Memory repository
│   └── SagaMessageHandler.cs        # Generic handler
├── EntityFramework/
│   ├── EntityFrameworkSagaRepository.cs  # EF Core repository with concurrency
│   ├── SagaDbContext.cs                  # Generic DbContext implementation
│   ├── SagaClassMap.cs                   # Base entity configuration
│   └── EntityFrameworkExtensions.cs      # DI registration extensions
└── DependencyInjection/
    └── HermesServiceCollectionExtensions.cs
```

---

## 🔗 Differences from MassTransit

| Feature                     | MassTransit Saga       | Hermes                |
|-----------------------------|------------------------|-----------------------|
| Transport integration       | RabbitMQ, Azure SB, etc | Transport-agnostic (bring your own) |
| Scheduling                  | Built-in (Quartz)      | Manual implementation |
| State persistence           | Entity Framework       | Entity Framework Core + custom implementations |
| Automatic retry             | Yes                    | Delegated to transport |
| Saga correlation            | Via message            | Via `CorrelationId`   |
| Optimistic concurrency      | Yes (with EF)          | Yes (automatic with EF) |
| Learning curve              | Steep                  | Gentle                |
| Dependencies                | Many                   | Minimal (EF Core optional) |

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
