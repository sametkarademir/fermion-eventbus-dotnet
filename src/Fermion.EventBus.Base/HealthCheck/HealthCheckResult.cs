using System.Text.Json.Serialization;

namespace Fermion.EventBus.Base.HealthCheck;

public class HealthCheckResult
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HealthStatus Status { get; set; }
    public string? Description { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();

    public static HealthCheckResult Healthy(string description = "Healthy") => new()
    {
        Status = HealthStatus.Healthy,
        Description = description
    };

    public static HealthCheckResult Unhealthy(string description, Exception? exception = null) => new()
    {
        Status = HealthStatus.Unhealthy,
        Description = description,
        Exception = exception
    };
}