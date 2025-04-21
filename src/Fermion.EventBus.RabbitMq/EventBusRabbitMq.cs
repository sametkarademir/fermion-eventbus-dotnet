using System.Net.Sockets;
using System.Text;
using Fermion.EventBus.Base;
using Fermion.EventBus.Base.Events;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Fermion.EventBus.RabbitMq;

public class EventBusRabbitMq : BaseEventBus
{
    private readonly ILogger<EventBusRabbitMq> _logger;
    private readonly RabbitMqPersistentConnection _persistentConnection;
    private readonly IModel _consumerChannel;

    public EventBusRabbitMq(EventBusConfig config, IServiceProvider serviceProvider) : base(config, serviceProvider)
    {
        _logger = serviceProvider.GetService(typeof(ILogger<EventBusRabbitMq>)) as ILogger<EventBusRabbitMq> ?? throw new InvalidOperationException();
        _logger.LogInformation("Initializing EventBusRabbitMq with config: DefaultTopicName={DefaultTopicName}, ConnectionRetryCount={ConnectionRetryCount}",
            config.DefaultTopicName,
            config.ConnectionRetryCount);

        IConnectionFactory? connectionFactory;
        if (config.Connection != null)
        {
            _logger.LogDebug("Creating ConnectionFactory from provided configuration");
            try
            {
                var connJson = JsonConvert.SerializeObject(EventBusConfig.Connection, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    TypeNameHandling = TypeNameHandling.Auto,
                });

                connectionFactory = JsonConvert.DeserializeObject<ConnectionFactory>(connJson, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    NullValueHandling = NullValueHandling.Ignore,
                });
                _logger.LogInformation("ConnectionFactory created successfully from configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create ConnectionFactory from configuration");
                throw;
            }
        }
        else
        {
            _logger.LogInformation("No connection configuration provided, using default ConnectionFactory");
            connectionFactory = new ConnectionFactory();
        }

        _persistentConnection = new RabbitMqPersistentConnection(connectionFactory, _logger, config.ConnectionRetryCount);

        _consumerChannel = CreateConsumerChannel();
        SubsManager.OnEventRemoved += SubsManagerOnOnEventRemoved;
        _logger.LogInformation("EventBusRabbitMq initialization completed successfully");
    }

    public RabbitMqPersistentConnection GetPersistentConnection() => _persistentConnection;
    
    private void SubsManagerOnOnEventRemoved(object? sender, string eventName)
    {
        _logger.LogInformation("Event removed from subscription manager: {EventName}", eventName);
        eventName = ProcessEventName(eventName);
        _logger.LogDebug("Processed event name: {ProcessedEventName}", eventName);

        if (!_persistentConnection.IsConnected)
        {
            _logger.LogWarning("Connection lost, attempting to reconnect...");
            _persistentConnection.TryConnect();
        }

        try
        {
            _consumerChannel.QueueUnbind(queue: eventName, exchange: EventBusConfig.DefaultTopicName, routingKey: eventName);
            _logger.LogInformation("Successfully unbound queue: {QueueName} from exchange: {Exchange} with routing key: {RoutingKey}",
                eventName,
                EventBusConfig.DefaultTopicName,
                eventName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unbind queue: {QueueName} from exchange: {Exchange}",
                eventName,
                EventBusConfig.DefaultTopicName);
            throw;
        }

        if (SubsManager.IsEmpty)
        {
            _logger.LogInformation("No more subscriptions, closing consumer channel");
            _consumerChannel.Close();
        }
    }

    private IModel CreateConsumerChannel()
    {
        if (!_persistentConnection.IsConnected)
        {
            _logger.LogWarning("Connection not established, attempting to connect...");
            _persistentConnection.TryConnect();
        }

        try
        {
            var channel = _persistentConnection.CreateModel();
            channel.ExchangeDeclare(exchange: EventBusConfig.DefaultTopicName, type: "direct");
            _logger.LogInformation("Successfully created consumer channel with exchange: {Exchange} of type: direct", EventBusConfig.DefaultTopicName);
            return channel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create consumer channel");
            throw;
        }
    }

    private void StartBaseConsume(string eventName)
    {
        _logger.LogInformation("Starting basic consume for event: {EventName}", eventName);

        try
        {
            var consumer = new EventingBasicConsumer(_consumerChannel);
            consumer.Received += ConsumerOnReceived;

            _consumerChannel.BasicConsume(
                queue: GetSubName(eventName),
                autoAck: false,
                consumer: consumer
            );

            _logger.LogInformation("Basic consumer started for queue: {QueueName}", GetSubName(eventName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start basic consumer for event: {EventName}", eventName);
            throw;
        }
    }

    private async void ConsumerOnReceived(object? sender, BasicDeliverEventArgs e)
    {
        var eventName = e.RoutingKey;
        var messageId = e.BasicProperties?.MessageId ?? Guid.NewGuid().ToString();

        _logger.LogInformation("Received message with routing key: {RoutingKey}, delivery tag: {DeliveryTag}, message id: {MessageId}",
            eventName,
            e.DeliveryTag,
            messageId);

        eventName = ProcessEventName(eventName);
        var message = Encoding.UTF8.GetString(e.Body.Span);

        _logger.LogDebug("Processing message content: {MessageContent}", message.Length > 1000 ? message.Substring(0, 1000) + "..." : message);

        try
        {
            await ProcessEvent(eventName, message);
            _consumerChannel.BasicAck(e.DeliveryTag, multiple: false);
            _logger.LogInformation("Successfully processed message with delivery tag: {DeliveryTag}", e.DeliveryTag);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error processing message with delivery tag: {DeliveryTag}, will nack and requeue", e.DeliveryTag);
            _consumerChannel.BasicNack(e.DeliveryTag, multiple: false, requeue: true);
            throw new InvalidOperationException("Error processing message", exception);
        }
    }

    public override void Publish(IntegrationEvent @event)
    {
        var eventName = @event.GetType().Name;
        _logger.LogInformation("Publishing event: {EventName}, event type: {EventType}", eventName, @event.GetType().FullName);

        if (!_persistentConnection.IsConnected)
        {
            _logger.LogWarning("Connection not established, attempting to connect before publishing...");
            _persistentConnection.TryConnect();
        }

        var policy = Policy.Handle<BrokerUnreachableException>()
            .Or<SocketException>()
            .WaitAndRetry(EventBusConfig.ConnectionRetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (ex, time) =>
                {
                    _logger.LogWarning(ex, "RabbitMQ Client could not connect after {TimeOut}s",
                        $"{time.TotalSeconds:n1}");
                });

        eventName = ProcessEventName(eventName);

        _consumerChannel.ExchangeDeclare(exchange: EventBusConfig.DefaultTopicName, type: "direct");

        var message = JsonConvert.SerializeObject(@event);
        var body = Encoding.UTF8.GetBytes(message);

        _logger.LogDebug("Serialized event to publish, message size: {MessageSize} bytes", body.Length);

        policy.Execute(() =>
        {
            var properties = _consumerChannel.CreateBasicProperties();
            properties.DeliveryMode = 2; // persistent
            properties.MessageId = Guid.NewGuid().ToString();
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _logger.LogDebug("Publishing to exchange: {Exchange} with routing key: {RoutingKey}, message id: {MessageId}",
                EventBusConfig.DefaultTopicName, eventName, properties.MessageId);

            _consumerChannel.BasicPublish(
                exchange: EventBusConfig.DefaultTopicName,
                routingKey: eventName,
                mandatory: true,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Successfully published event: {EventName} with message id: {MessageId}",
                eventName, properties.MessageId);
        });
    }

    public override void Subscribe<T, TH>()
    {
        var eventName = typeof(T).Name;
        _logger.LogInformation("Subscribing to event: {EventName} with handler: {HandlerType}",
            eventName, typeof(TH).FullName);

        eventName = ProcessEventName(eventName);

        if (!SubsManager.HasSubscriptionForEvent(eventName))
        {
            _logger.LogInformation("No existing subscription for event: {EventName}, creating queue and binding", eventName);

            if (!_persistentConnection.IsConnected)
            {
                _logger.LogWarning("Connection not established, attempting to connect before subscribing...");
                _persistentConnection.TryConnect();
            }

            try
            {
                _consumerChannel.QueueDeclare(
                    queue: GetSubName(eventName),
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                _logger.LogInformation("Queue declared: {QueueName}", GetSubName(eventName));

                _consumerChannel.QueueBind(
                    queue: GetSubName(eventName),
                    exchange: EventBusConfig.DefaultTopicName,
                    routingKey: eventName
                );

                _logger.LogInformation("Queue bound: {QueueName} to exchange: {Exchange} with routing key: {RoutingKey}",
                    GetSubName(eventName), EventBusConfig.DefaultTopicName, eventName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to event: {EventName}", eventName);
                throw;
            }
        }
        else
        {
            _logger.LogDebug("Subscription already exists for event: {EventName}", eventName);
        }

        SubsManager.AddSubscription<T, TH>();
        StartBaseConsume(eventName);

        _logger.LogInformation("Successfully subscribed to event: {EventName}", eventName);
    }

    public override void UnSubscribe<T, TH>()
    {
        var eventName = typeof(T).Name;
        _logger.LogInformation("Unsubscribing from event: {EventName} with handler: {HandlerType}",
            eventName, typeof(TH).FullName);

        try
        {
            SubsManager.RemoveSubscription<T, TH>();
            _logger.LogInformation("Successfully unsubscribed from event: {EventName}", eventName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from event: {EventName}", eventName);
            throw;
        }
    }
}