namespace Fermion.EventBus.Base.Exceptions;

public class EventBusPublishException : EventBusException
{
    public EventBusPublishException(string message) : base(message) { }
    public EventBusPublishException(string message, Exception innerException) : base(message, innerException) { }
}