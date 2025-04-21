using Fermion.EventBus.Base;
using RabbitMQ.Client;

namespace Fermion.EventBus.RabbitMq.Extensions;

public static class EventBusConfigExtensions
{
    public static EventBusConfig.Builder WithRabbitMqConnection(this EventBusConfig.Builder builder, string host, int port, string userName, string password)
    {
        builder.WithConnection(new ConnectionFactory()
        {
            HostName = host,
            Port = port,
            UserName = userName,
            Password = password
        })
            .UseRabbitMq();
        return builder;
    }
}