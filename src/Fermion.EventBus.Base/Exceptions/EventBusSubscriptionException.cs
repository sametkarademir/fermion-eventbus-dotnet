namespace Fermion.EventBus.Base.Exceptions;

public class EventBusSubscriptionException : EventBusException
{
    public EventBusSubscriptionException(string message) : base(message) { }
    public EventBusSubscriptionException(string message, Exception innerException) : base(message, innerException) { }
}