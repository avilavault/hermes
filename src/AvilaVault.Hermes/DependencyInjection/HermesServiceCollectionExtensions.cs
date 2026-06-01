using AvilaVault.Hermes.Abstractions;
using AvilaVault.Hermes.InMemory;
using AvilaVault.Hermes.EntityFramework;
using AvilaVault.Hermes.StateMachine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AvilaVault.Hermes.DependencyInjection;

public static class HermesServiceCollectionExtensions
{
    public static IServiceCollection AddHermes(
        this IServiceCollection services,
        Action<HermesConfigurator> configure)
    {
        services.AddSingleton<IHermesBus, InMemoryHermesBus>();

        var configurator = new HermesConfigurator(services);
        configure(configurator);

        return services;
    }
}

public sealed class HermesConfigurator
{
    private readonly IServiceCollection _services;

    internal HermesConfigurator(IServiceCollection services)
        => _services = services;

    public SagaConfigurator<TStateMachine, TSaga> AddSagaStateMachine<TStateMachine, TSaga>()
        where TStateMachine : HermesStateMachine<TSaga>
        where TSaga : class, ISagaState, new()
    {
        _services.AddSingleton<TStateMachine>();
        return new SagaConfigurator<TStateMachine, TSaga>(_services);
    }
}

public sealed class SagaConfigurator<TStateMachine, TSaga>
    where TStateMachine : HermesStateMachine<TSaga>
    where TSaga : class, ISagaState, new()
{
    private readonly IServiceCollection _services;

    internal SagaConfigurator(IServiceCollection services)
        => _services = services;

    /// <summary>
    /// Usa repositório in-memory (ConcurrentDictionary).
    /// Registra automaticamente um ISagaMessageHandler para cada tipo de evento
    /// que a machine declarou via Initially()/During().
    /// </summary>
    public SagaConfigurator<TStateMachine, TSaga> InMemory()
    {
        _services.AddSingleton<ISagaRepository<TSaga>, InMemorySagaRepository<TSaga>>();

        // Instanciamos a machine temporariamente para descobrir os tipos de evento registrados.
        // Isso é seguro pois a machine só declara transições no construtor.
        var tempMachine = Activator.CreateInstance<TStateMachine>();
        var eventTypes = tempMachine.GetRegisteredEventTypes();

        foreach (var eventType in eventTypes)
        {
            var handlerInterface = typeof(ISagaMessageHandler<>).MakeGenericType(eventType);
            var handlerImpl = typeof(SagaMessageHandler<,,>)
                                    .MakeGenericType(typeof(TStateMachine), typeof(TSaga), eventType);

            _services.AddSingleton(handlerInterface, handlerImpl);
        }

        return this;
    }

    /// <summary>
    /// Usa Entity Framework Core para persistência.
    /// Cria automaticamente um DbContext dedicado para esta saga.
    /// </summary>
    /// <param name="configureDbContext">
    /// Configuração do DbContext — geralmente UseSqlServer/UseNpgsql/UseSqlite.
    /// Exemplo: opt => opt.UseSqlServer(connectionString)
    /// </param>
    public SagaConfigurator<TStateMachine, TSaga> EntityFramework(
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        _services.AddDbContext<ISagaDbContext, SagaDbContext<TSaga>>(configureDbContext);

        _services.AddScoped<ISagaRepository<TSaga>, EntityFrameworkSagaRepository<TSaga>>();

        var tempMachine = Activator.CreateInstance<TStateMachine>();
        var eventTypes = tempMachine.GetRegisteredEventTypes();

        foreach (var eventType in eventTypes)
        {
            var handlerInterface = typeof(ISagaMessageHandler<>).MakeGenericType(eventType);
            var handlerImpl = typeof(SagaMessageHandler<,,>)
                                    .MakeGenericType(typeof(TStateMachine), typeof(TSaga), eventType);

            _services.AddScoped(handlerInterface, handlerImpl);
        }

        return this;
    }

    /// <summary>
    /// Usa Entity Framework Core com DbContext customizado fornecido pelo usuário.
    /// Use quando você já tem um DbContext que inclui múltiplas sagas.
    /// </summary>
    /// <typeparam name="TDbContext">Tipo do DbContext customizado</typeparam>
    public SagaConfigurator<TStateMachine, TSaga> EntityFramework<TDbContext>()
        where TDbContext : DbContext, ISagaDbContext
    {
        _services.AddScoped<ISagaRepository<TSaga>, EntityFrameworkSagaRepository<TSaga>>();

        var tempMachine = Activator.CreateInstance<TStateMachine>();
        var eventTypes = tempMachine.GetRegisteredEventTypes();

        foreach (var eventType in eventTypes)
        {
            var handlerInterface = typeof(ISagaMessageHandler<>).MakeGenericType(eventType);
            var handlerImpl = typeof(SagaMessageHandler<,,>)
                                    .MakeGenericType(typeof(TStateMachine), typeof(TSaga), eventType);

            _services.AddScoped(handlerInterface, handlerImpl);
        }

        return this;
    }
}