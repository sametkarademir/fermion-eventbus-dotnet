using System.Net.Sockets;
using Fermion.EventBus.Base.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Fermion.EventBus.RabbitMq;

public class RabbitMqPersistentConnection : IDisposable
{
    private readonly ILogger<EventBusRabbitMq> _logger;
    private IConnection _connection;
    public bool IsConnected => _connection != null && _connection.IsOpen;
    private readonly IConnectionFactory? _connectionFactory;
    private readonly int _retryCount;
    private readonly object _lockObject = new object();
    private bool _disposed;

    public RabbitMqPersistentConnection(IConnectionFactory? connectionFactory, ILogger<EventBusRabbitMq> logger, int retryCount = 5)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _retryCount = retryCount;

        _logger.LogInformation("RabbitMqPersistentConnection initialized with retry count: {RetryCount}", retryCount);
    }

    public IModel CreateModel()
    {
        if (!IsConnected)
        {
            _logger.LogError("Cannot create model: connection is not established.");
            throw new EventBusConnectionException("No RabbitMQ connections are available to perform this action");
        }

        try
        {
            var model = _connection.CreateModel();
            _logger.LogDebug("Successfully created RabbitMQ model/channel");
            return model;
        }
        catch (Exception? ex)
        {
            _logger.LogError(ex, "Failed to create RabbitMQ model/channel");
            throw;
        }
    }

    public bool TryConnect()
    {
        _logger.LogInformation("Attempting to connect to RabbitMQ broker...");

        lock (_lockObject)
        {
            if (IsConnected)
            {
                _logger.LogDebug("Already connected to RabbitMQ broker, skipping connection attempt");
                return true;
            }

            var policy = Policy.Handle<SocketException>()
                .Or<BrokerUnreachableException>()
                .WaitAndRetry(_retryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, time, retryCount, context) =>
                    {
                        _logger.LogWarning(ex,
                            "RabbitMQ Client connection attempt {RetryCount} of {MaxRetryCount} failed after {TimeOut}s. Exception: {ExceptionMessage}",
                            retryCount, _retryCount, $"{time.TotalSeconds:n1}", ex.Message);
                    });

            try
            {
                policy.Execute(() =>
                {
                    if (_connectionFactory != null)
                    {
                        _logger.LogDebug("Creating connection using provided connection factory");
                        _connection = _connectionFactory.CreateConnection();
                    }
                    else
                    {
                        _logger.LogError("ConnectionFactory is null, cannot create connection");
                        throw new EventBusConnectionException("ConnectionFactory must not be null");
                    }
                });
            }
            catch (Exception? ex)
            {
                _logger.LogError(ex, "All connection attempts to RabbitMQ failed");
                return false;
            }

            if (IsConnected)
            {
                _connection.ConnectionShutdown += ConnectionOnConnectionShutdown;
                _connection.CallbackException += ConnectionOnCallbackException;
                _connection.ConnectionBlocked += ConnectionOnConnectionBlocked;

                _logger.LogInformation(
                    "RabbitMQ Client acquired a persistent connection to '{HostName}' and is subscribed to failure events",
                    _connection.Endpoint.HostName);

                return true;
            }
            else
            {
                _logger.LogError("Failed to establish connection to RabbitMQ broker");
                return false;
            }
        }
    }

    private void ConnectionOnConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
    {
        _logger.LogWarning("RabbitMQ connection is blocked. Reason: {Reason}", e.Reason);

        if (_disposed)
        {
            _logger.LogDebug("Connection is disposed, skipping reconnection attempt");
            return;
        }

        _logger.LogInformation("Attempting to reconnect to RabbitMQ after connection was blocked");
        TryConnect();
    }

    private void ConnectionOnCallbackException(object? sender, CallbackExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "RabbitMQ connection callback exception occurred. Details: {Details}", e.Detail);

        if (_disposed)
        {
            _logger.LogDebug("Connection is disposed, skipping reconnection attempt");
            return;
        }

        _logger.LogInformation("Attempting to reconnect to RabbitMQ after callback exception");
        TryConnect();
    }

    private void ConnectionOnConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning(
            "RabbitMQ connection is shutdown. Initiator: {Initiator}, ReplyCode: {ReplyCode}, ReplyText: {ReplyText}",
            e.Initiator, e.ReplyCode, e.ReplyText);

        if (_disposed)
        {
            _logger.LogDebug("Connection is disposed, skipping reconnection attempt");
            return;
        }

        _logger.LogInformation("Attempting to reconnect to RabbitMQ after connection shutdown");
        TryConnect();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing RabbitMQ connection");

        _disposed = true;

        try
        {
            if (_connection != null)
            {
                _connection.ConnectionShutdown -= ConnectionOnConnectionShutdown;
                _connection.CallbackException -= ConnectionOnCallbackException;
                _connection.ConnectionBlocked -= ConnectionOnConnectionBlocked;

                _connection.Dispose();
                _logger.LogInformation("RabbitMQ connection disposed successfully");
            }
        }
        catch (Exception? ex)
        {
            _logger.LogError(ex, "Error while disposing RabbitMQ connection");
        }
    }
}