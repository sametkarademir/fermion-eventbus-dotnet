using Fermion.EventBus.Base;
using Fermion.EventBus.Base.Abstraction;
using Fermion.EventBus.RabbitMq;

namespace Fermion.EventBus.Factory;

public static class EventBusFactory
{
    public static IEventBus Create(EventBusConfig config, IServiceProvider serviceProvider)
    {
        return config.EventBusType switch
        {
            EventBusType.RabbitMq => new EventBusRabbitMq(config, serviceProvider),
            _ => new EventBusRabbitMq(config, serviceProvider),
        };
    }
}