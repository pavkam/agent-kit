// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ErrorOriginTests: Conformance.StringIdentityConformanceTests<ErrorOrigin>
{

    /// <inheritdoc/>
    protected override ErrorOrigin Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ErrorOrigin subject) => subject.Value;

    [Fact]
    public void Constructor_WhenIdentityTextNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ErrorOrigin(null!)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenIdentityTextBlank_ThrowsArgumentException()
    {
        foreach (var text in new[] { string.Empty, " ", "\t" })
        {
            var exception = Should.Throw<ArgumentException>(() => new ErrorOrigin(text));
            _ = exception.ShouldBeOfType<ArgumentException>();
            exception.ParamName.ShouldBe("value");
        }
    }

    [Fact]
    public void Constructor_WhenIdentityTextValid_PreservesOrdinalValueEqualityAndDefaultFormatting()
    {
        var value = new ErrorOrigin("Provider-A");
        var same = new ErrorOrigin("Provider-A");
        value.Value.ShouldBe("Provider-A");
        value.ShouldBe(same);
        value.GetHashCode().ShouldBe(same.GetHashCode());
        value.ShouldNotBe(new ErrorOrigin("provider-a"));
        value.ToString().ShouldBe("Provider-A");
        default(ErrorOrigin).ToString().ShouldBe(string.Empty);
    }
}
