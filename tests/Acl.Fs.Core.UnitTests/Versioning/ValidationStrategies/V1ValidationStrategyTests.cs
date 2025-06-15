using Acl.Fs.Core.Interfaces;
using Acl.Fs.Core.Versioning.ValidationStrategies;

namespace Acl.Fs.Core.UnitTests.Versioning.ValidationStrategies;

public sealed class V1ValidationStrategyTests
{
    private readonly V1ValidationStrategy _strategy = new();

    [Fact]
    public void Validate_MinorVersion0_DoesNotThrow()
    {
        var exception = Record.Exception(() => _strategy.Validate(0));
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_MinorVersion1_DoesNotThrow()
    {
        var exception = Record.Exception(() => _strategy.Validate(1));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(255)]
    public void Validate_AnyMinorVersion_DoesNotThrow(byte minorVersion)
    {
        var exception = Record.Exception(() => _strategy.Validate(minorVersion));
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_RepeatedCalls_ConsistentBehavior()
    {
        for (var i = 0; i < 10; i++)
        {
            var i1 = i;
            var exception = Record.Exception(() => _strategy.Validate((byte)i1));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void Validate_MaxMinorVersion_DoesNotThrow()
    {
        var exception = Record.Exception(() => _strategy.Validate(byte.MaxValue));
        Assert.Null(exception);
    }

    [Fact]
    public void V1ValidationStrategy_ImplementsInterface()
    {
        Assert.IsType<IVersionValidationStrategy>(_strategy, false);
    }

    [Fact]
    public void Constructor_CreatesValidInstance()
    {
        var strategy = new V1ValidationStrategy();

        Assert.NotNull(strategy);
    }

    [Fact]
    public void Validate_DoesNotModifyState()
    {
        var strategy1 = new V1ValidationStrategy();
        var strategy2 = new V1ValidationStrategy();

        strategy1.Validate(1);
        strategy1.Validate(2);
        strategy2.Validate(3);

        var exception1 = Record.Exception(() => strategy1.Validate(10));
        var exception2 = Record.Exception(() => strategy2.Validate(20));

        Assert.Null(exception1);
        Assert.Null(exception2);
    }

    [Fact]
    public void Validate_MultipleInstances_IndependentBehavior()
    {
        var strategy1 = new V1ValidationStrategy();
        var strategy2 = new V1ValidationStrategy();
        var strategy3 = new V1ValidationStrategy();

        var exception1 = Record.Exception(() => strategy1.Validate(100));
        var exception2 = Record.Exception(() => strategy2.Validate(200));
        var exception3 = Record.Exception(() => strategy3.Validate(50));

        Assert.Null(exception1);
        Assert.Null(exception2);
        Assert.Null(exception3);
    }
}