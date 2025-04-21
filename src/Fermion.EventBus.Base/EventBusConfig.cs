namespace Fermion.EventBus.Base;

public class EventBusConfig
{
    public int ConnectionRetryCount { get; set; } = 5;
    public string DefaultTopicName { get; set; } = "DefaultTopicName";
    public string SubscriberClientAppName { get; set; } = String.Empty;
    public string EventNamePrefix { get; set; } = string.Empty;
    public string EventNameSuffix { get; set; } = "IntegrationEvent";
    public EventBusType EventBusType { get; set; } = EventBusType.RabbitMq;
    public object? Connection { get; set; }

    public bool DeleteEventPrefix => !string.IsNullOrEmpty(EventNamePrefix);
    public bool DeleteEventSuffix => !string.IsNullOrEmpty(EventNameSuffix);


    public class Builder
    {
        private readonly EventBusConfig _config = new EventBusConfig();

        public Builder WithConnectionRetryCount(int retryCount)
        {
            _config.ConnectionRetryCount = retryCount;
            return this;
        }

        public Builder WithDefaultTopicName(string topicName)
        {
            _config.DefaultTopicName = topicName;
            return this;
        }

        public Builder WithSubscriberClientAppName(string appName)
        {
            _config.SubscriberClientAppName = appName;
            return this;
        }

        public Builder WithEventNamePrefix(string prefix)
        {
            _config.EventNamePrefix = prefix;
            return this;
        }

        public Builder WithEventNameSuffix(string suffix)
        {
            _config.EventNameSuffix = suffix;
            return this;
        }

        public Builder UseRabbitMq()
        {
            _config.EventBusType = EventBusType.RabbitMq;
            return this;
        }

        public Builder WithConnection(object connection)
        {
            _config.Connection = connection;
            return this;
        }

        public EventBusConfig Build()
        {
            return _config;
        }
    }

    public static Builder CreateBuilder()
    {
        return new Builder();
    }

    public void Validate()
    {
        if (string.IsNullOrEmpty(SubscriberClientAppName))
            throw new ArgumentException("SubscriberClientAppName is required");
    }

    public static EventBusConfig CreateDefaultRabbitMqConfig()
    {
        return new EventBusConfig
        {
            EventBusType = EventBusType.RabbitMq,
            ConnectionRetryCount = 5,
            DefaultTopicName = "DefaultTopicName",
            EventNameSuffix = "IntegrationEvent",
            SubscriberClientAppName = AppDomain.CurrentDomain.FriendlyName
        };
    }
}