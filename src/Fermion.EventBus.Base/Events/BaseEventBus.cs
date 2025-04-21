using Fermion.EventBus.Base.Abstraction;
using Fermion.EventBus.Base.SubManagers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Fermion.EventBus.Base.Events;

public abstract class BaseEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    protected readonly IEventBusSubscriptionManager SubsManager;

    protected EventBusConfig EventBusConfig { get; set; }

    protected BaseEventBus(EventBusConfig config, IServiceProvider serviceProvider)
    {
        EventBusConfig = config;
        _serviceProvider = serviceProvider;
        SubsManager = new InMemoryEventBusSubscriptionManager(ProcessEventName);
    }

    protected string ProcessEventName(string eventName)
    {
        if (EventBusConfig.DeleteEventPrefix)
        {
            eventName = eventName.TrimStart(EventBusConfig.EventNamePrefix.ToArray());
        }

        if (EventBusConfig.DeleteEventSuffix)
        {
            var array = EventBusConfig.EventNameSuffix.ToArray();
            eventName = eventName.TrimEnd(array);
        }

        return eventName;
    }

    protected string GetSubName(string eventName)
    {
        return $"{EventBusConfig.SubscriberClientAppName}.{ProcessEventName(eventName)}";
    }

    public virtual void Dispose()
    {
        EventBusConfig = null;
        SubsManager.Clear();
    }

    protected async Task ProcessEvent(string eventName, string message)
    {
        eventName = ProcessEventName(eventName);
        if (SubsManager.HasSubscriptionForEvent(eventName))
        {
            var subscriptions = SubsManager.GetHandlersForEvent(eventName);
            using var scope = _serviceProvider.CreateScope();
            foreach (var subscription in subscriptions)
            {
                var handler = _serviceProvider.GetService(subscription.HandlerType);
                if (handler == null)
                {
                    continue;
                }

                var eventType =
                    SubsManager.GetEventTypeByName(
                        $"{EventBusConfig.EventNamePrefix}{eventName}{EventBusConfig.EventNameSuffix}");
                var integrationEvent = JsonConvert.DeserializeObject(message, eventType);

                var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                await (Task)concreteType.GetMethod("Handle").Invoke(handler, [integrationEvent]);
            }
        }
    }

    public abstract void Publish(IntegrationEvent @event);

    public abstract void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;

    public abstract void UnSubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
}