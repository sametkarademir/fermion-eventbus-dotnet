namespace Fermion.EventBus.Base.HealthCheck;

public interface IEventBusHealthCheck
{
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}