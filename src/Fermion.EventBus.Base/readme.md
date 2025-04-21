# Fermion.EventBus

A modular and extensible event bus implementation for distributed .NET applications, with built-in support for RabbitMQ.

## Features

- 🚀 Message broker abstraction layer
- 🐰 RabbitMQ integration
- 💪 Resilient connection handling
- 🔄 Automatic reconnection with exponential backoff
- 🏥 Health check integration
- 🔍 Comprehensive logging
- 📊 Metadata enrichment for events
- 🏗️ Factory pattern for easy instantiation
- 🛠️ Flexible configuration via builder pattern

## Installation

### NuGet Installation

```bash
dotnet add package Fermion.EventBus.Base
dotnet add package Fermion.EventBus.RabbitMq
dotnet add package Fermion.EventBus.Factory
```

## Quick Start

### 1. Define your event

```csharp
using Fermion.EventBus.Base.Events;

public class MailSendRequestEvent : IntegrationEvent
{
    public MailSendRequestEvent(string to, string subject, string body, string name)
    {
        To = to;
        Subject = subject;
        Body = body;
        Name = name;
    }

    public string Name { get; set; }
    public string To { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
}
```

### 2. Create an event handler

```csharp
using Fermion.EventBus.Base.Abstraction;

public class MailSendRequestEventHandler : IIntegrationEventHandler<MailSendRequestEvent>
{
    private readonly ILogger<MailSendRequestEventHandler> _logger;

    public MailSendRequestEventHandler(ILogger<MailSendRequestEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(MailSendRequestEvent @event)
    {
        _logger.LogInformation("Processing email request for {Recipient}", @event.To);
        // Simulate sending email
        await Task.Delay(1000);
        _logger.LogInformation("Email sent to {Recipient} with subject {Subject}", @event.To, @event.Subject);
    }
}
```

### 3. Configure and register the event bus

```csharp
// In your Program.cs or Startup.cs

builder.Services.AddTransient<MailSendRequestEventHandler>();

var config = EventBusConfig.CreateBuilder()
    .WithConnectionRetryCount(5)
    .WithSubscriberClientAppName(AppDomain.CurrentDomain.FriendlyName)
    .WithDefaultTopicName("IdentityUser")
    .WithEventNameSuffix(string.Empty)
    .WithEventNamePrefix(string.Empty)
    .WithRabbitMqConnection(
        host: builder.Configuration["RabbitMq:Host"] ?? "localhost",
        port: Convert.ToInt16(builder.Configuration["RabbitMq:Port"] ?? "5672"),
        userName: builder.Configuration["RabbitMq:UserName"] ?? "guest",
        password: builder.Configuration["RabbitMq:Password"] ?? "guest"
    )
    .Build();

builder.Services.AddSingleton<IEventBus>(sp => EventBusFactory.Create(config, sp));
builder.Services.AddSingleton<IEventBusHealthCheck>(sp =>
{
    var eventBus = sp.GetRequiredService<IEventBus>();
    return EventBusFactory.CreateHealthCheck(eventBus, sp);
});
```

### 4. Subscribe to events

```csharp
// After building your application
var app = builder.Build();

IEventBus eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<MailSendRequestEvent, MailSendRequestEventHandler>();
```

### 5. Publish events

```csharp
// Anywhere in your application
var eventBus = serviceProvider.GetRequiredService<IEventBus>();
var mailEvent = new MailSendRequestEvent("user@example.com", "Welcome", "Hello World", "John Doe");
eventBus.Publish(mailEvent);
```

## Configuration

### Builder Configuration

The `EventBusConfig.CreateBuilder()` method provides a fluent API for configuring the event bus:

```csharp
var config = EventBusConfig.CreateBuilder()
    .WithConnectionRetryCount(5)                    // Number of connection retry attempts
    .WithSubscriberClientAppName("MyService")       // Subscriber identifier
    .WithDefaultTopicName("MyTopic")                // Default exchange/topic name
    .WithEventNameSuffix("Event")                   // Suffix for event names
    .WithEventNamePrefix("My.")                     // Prefix for event names
    .Build();
```

### RabbitMQ Connection

Configure RabbitMQ connection using the `WithRabbitMqConnection` method:

```csharp
.WithRabbitMqConnection(
    host: "localhost",
    port: 5672,
    userName: "guest",
    password: "guest"
)
```

### Advanced Configuration

```csharp
var config = new EventBusConfig
{
    ConnectionRetryCount = 5,
    SubscriberClientAppName = "MyService",
    DefaultTopicName = "MyTopic",
    EventBusType = EventBusType.RabbitMq,
    EventNameSuffix = "Event",
    Connection = new ConnectionFactory()
    {
        HostName = "localhost",
        Port = 5672,
        UserName = "guest",
        Password = "guest"
    }
};
```

## Health Checks

The event bus includes built-in health check support:

```csharp
// Add health check to your application
app.MapGet("/health/eventbus", async (IEventBusHealthCheck healthCheck) =>
{
    var result = await healthCheck.CheckHealthAsync();
    return Results.Json(result);
});
```

### Custom Exception Types

The library provides specific exception types for better error handling:

- `EventBusException` - Base exception for all event bus errors
- `EventBusConnectionException` - Connection-related errors
- `EventBusSubscriptionException` - Subscription-related errors
- `EventBusPublishException` - Publishing-related errors

## Event Metadata

Events automatically include metadata:

- `EventId` - Unique identifier for the event
- `CreatedDate` - Timestamp when the event was created
- `AppName` - Application name that published the event
- `MachineName` - Machine name that published the event
- `Environment` - Environment (Development, Production, etc.)
- `CorrelationId` - For tracking requests across services
- `TenantId` - For multi-tenant applications
- `SessionId` - For session tracking
- `UserInfo` - For user context

## Extending the Library

The library is designed to be extensible. You can create your own message broker implementations by:

1. Implementing the `IEventBus` interface
2. Extending the `BaseEventBus` class
3. Adding your implementation to the `EventBusFactory`

## Best Practices

1. **Use specific event types** - Create specific event classes for each use case
2. **Handle errors gracefully** - Implement error handling in your event handlers
3. **Use correlation IDs** - Track requests across services
4. **Monitor DLQ** - Regular monitoring of Dead Letter Queues
5. **Keep handlers idempotent** - Design handlers to handle duplicate messages

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For issues and feature requests, please use the GitHub issue tracker.