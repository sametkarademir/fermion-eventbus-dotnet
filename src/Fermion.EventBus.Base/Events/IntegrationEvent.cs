using Newtonsoft.Json;

namespace Fermion.EventBus.Base.Events;

public class IntegrationEvent
{
    [JsonProperty]
    public Guid EventId { get; private set; }

    [JsonProperty]
    public DateTime CreatedDate { get; private set; }

    [JsonProperty]
    public string AppName { get; protected set; }

    [JsonProperty]
    public string MachineName { get; protected set; }

    [JsonProperty]
    public string Environment { get; protected set; }

    [JsonProperty]
    public Guid? CorrelationId { get; set; }

    [JsonProperty]
    public string? TenantId { get; set; }

    [JsonProperty]
    public string? SessionId { get; set; }

    [JsonProperty]
    public string? UserInfo { get; set; }

    protected IntegrationEvent(
        Guid? correlationId = null,
        string? tenantId = null,
        string? sessionId = null,
        string? userInfo = null)
    {
        EventId = Guid.NewGuid();
        CreatedDate = DateTime.UtcNow;
        AppName = AppDomain.CurrentDomain.FriendlyName;
        MachineName = System.Environment.MachineName;
        Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        CorrelationId = correlationId;
        TenantId = tenantId;
        SessionId = sessionId;
        UserInfo = userInfo;
    }
}