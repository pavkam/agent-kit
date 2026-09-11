// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class AgentErrorCodeTests: Conformance.StringIdentityConformanceTests<AgentErrorCode>
{

    /// <inheritdoc/>
    protected override AgentErrorCode Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(AgentErrorCode subject) => subject.Value;

    [Fact]
    public void Constructor_WhenIdentityTextNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentErrorCode(null!)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenIdentityTextBlank_ThrowsArgumentException()
    {
        foreach (var text in new[] { string.Empty, " ", "\t" })
        {
            var exception = Should.Throw<ArgumentException>(() => new AgentErrorCode(text));
            _ = exception.ShouldBeOfType<ArgumentException>();
            exception.ParamName.ShouldBe("value");
        }
    }

    [Fact]
    public void Constructor_WhenIdentityTextValid_PreservesOrdinalValueEqualityAndDefaultFormatting()
    {
        var value = new AgentErrorCode("Provider-A");
        var same = new AgentErrorCode("Provider-A");
        value.Value.ShouldBe("Provider-A");
        value.ShouldBe(same);
        value.GetHashCode().ShouldBe(same.GetHashCode());
        value.ShouldNotBe(new AgentErrorCode("provider-a"));
        value.ToString().ShouldBe("Provider-A");
        default(AgentErrorCode).ToString().ShouldBe(string.Empty);
    }
}
