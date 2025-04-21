using Fermion.EventBus.Base.HealthCheck;
using Microsoft.Extensions.Logging;

namespace Fermion.EventBus.RabbitMq.HealthCheck;

public class RabbitMqEventBusHealthCheck : IEventBusHealthCheck
{
    private readonly RabbitMqPersistentConnection _connection;
    private readonly ILogger<RabbitMqEventBusHealthCheck> _logger;
    
    public RabbitMqEventBusHealthCheck(RabbitMqPersistentConnection connection, ILogger<RabbitMqEventBusHealthCheck> logger)
    {
        _connection = connection;
        _logger = logger;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_connection.IsConnected)
            {
                using var channel = _connection.CreateModel();
                
                return HealthCheckResult.Healthy("RabbitMQ connection is healthy");
            }

            return HealthCheckResult.Unhealthy("RabbitMQ connection is not established");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ health check failed");
            return HealthCheckResult.Unhealthy("RabbitMQ health check failed", ex);
        }
    }
}