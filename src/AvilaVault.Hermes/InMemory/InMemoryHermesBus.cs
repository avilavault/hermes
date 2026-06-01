using AvilaVault.Hermes.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AvilaVault.Hermes.InMemory;

internal sealed class InMemoryHermesBus : IHermesBus
{
    private readonly IServiceProvider _provider;

    public InMemoryHermesBus(IServiceProvider provider)
        => _provider = provider;

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken ct = default)
        where TMessage : class, ICorrelatedMessage
    {
        var handlers = _provider.GetServices<ISagaMessageHandler<TMessage>>();

        foreach (var handler in handlers)
            await handler.HandleAsync(message, ct);
    }
}