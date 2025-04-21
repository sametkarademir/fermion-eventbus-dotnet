namespace Fermion.EventBus.Base.Exceptions;

public class EventBusConnectionException : EventBusException
{
    public EventBusConnectionException(string message) : base(message) { }
    public EventBusConnectionException(string message, Exception innerException) : base(message, innerException) { }
}