using Fermion.EventBus.Base;
using Fermion.EventBus.Base.Abstraction;
using Fermion.EventBus.Base.HealthCheck;
using Fermion.EventBus.RabbitMq;
using Fermion.EventBus.RabbitMq.HealthCheck;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    public static IEventBusHealthCheck CreateHealthCheck(IEventBus eventBus, IServiceProvider serviceProvider)
    {
        if (eventBus is EventBusRabbitMq rabbitMqEventBus)
        {
            return new RabbitMqEventBusHealthCheck(rabbitMqEventBus.GetPersistentConnection(), 
                serviceProvider.GetRequiredService<ILogger<RabbitMqEventBusHealthCheck>>());
        }
        throw new ArgumentException("Health check is only supported for RabbitMQ EventBus");
    }
}