namespace TestingCore;

public interface IClock { DateTimeOffset UtcNow { get; } }

public sealed class TransferPolicy(IClock clock)
{
    public bool CanSubmit(decimal amount, decimal balance) =>
        amount > 0 && amount <= balance &&
        clock.UtcNow.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
}