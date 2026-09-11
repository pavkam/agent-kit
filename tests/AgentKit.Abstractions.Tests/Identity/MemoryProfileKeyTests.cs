// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies MemoryProfileKey behavior and contracts.</summary>
public sealed class MemoryProfileKeyTests: Conformance.StringIdentityConformanceTests<MemoryProfileKey>
{
    /// <inheritdoc/>
    protected override MemoryProfileKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(MemoryProfileKey subject) => subject.Value;
    [Fact]
    public void DefaultValues_HaveNoUsableSelectionText()
    {
        default(MemoryProfileKey).Value.ShouldBeNull();
        default(MemoryProfileKey).ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenValueIsValid_RoundTripsWithStructuralEquality()
    {
        var first = new MemoryProfileKey("primary");
        var second = new MemoryProfileKey("primary");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldBe("primary");
        new MemoryProfileKey("secondary").ShouldNotBe(first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenValueIsBlank_ThrowsExactArgumentException(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new MemoryProfileKey(value!));
        _ = value is null ? exception.ShouldBeOfType<ArgumentNullException>() : exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_PreservesExactTextAndUsesOrdinalCaseSensitiveEquality()
    {
        var preserved = new MemoryProfileKey(" Primary ");
        preserved.ToString().ShouldBe(" Primary ");
        new MemoryProfileKey("primary").ShouldNotBe(new MemoryProfileKey("PRIMARY"));
    }
}
