namespace DeliveryHub.Domain.SharedKernel;

public abstract record DomainEvent(Guid Id, DateTimeOffset OccurredOn);
