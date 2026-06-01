namespace AvilaVault.Hermes.StateMachine;

/// <summary>
/// Representa um evento tipado dentro da state machine.
/// É usado apenas como "marcador" na DSL — o tipo TMessage é o que importa em runtime.
/// </summary>
public sealed class Event<TMessage>
{
    // Intencionalmenete vazio: a identidade do evento é o tipo TMessage.
    // Manter a instância como propriedade na machine permite a DSL fluente:
    //   .When(OrderPlaced) em vez de .When<OrderPlaced>()
}