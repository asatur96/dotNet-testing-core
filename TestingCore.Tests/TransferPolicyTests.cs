using TestingCore;

namespace TestingCore.Tests;

public sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
}

public sealed class TransferFixture
{
    public FixedClock Clock { get; } = new();
    public TransferPolicy Policy => new(Clock);
}

public sealed class TransferPolicyTests(TransferFixture fixture) : IClassFixture<TransferFixture>
{
    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(101, 100, false)]
    [InlineData(0, 100, false)]
    [InlineData(-1, 100, false)]
    public void CanSubmit_checks_amount_and_balance(decimal amount, decimal balance, bool expected)
    {
        fixture.Clock.UtcNow = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, fixture.Policy.CanSubmit(amount, balance));
    }

    [Fact]
    public void CanSubmit_rejects_weekend()
    {
        fixture.Clock.UtcNow = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        Assert.False(fixture.Policy.CanSubmit(50, 100));
    }
}