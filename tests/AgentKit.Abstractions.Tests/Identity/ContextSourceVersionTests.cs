// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ContextSourceVersion behavior and contracts.</summary>
public sealed class ContextSourceVersionTests: Conformance.StringIdentityConformanceTests<ContextSourceVersion>
{
    /// <inheritdoc/>
    protected override ContextSourceVersion Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ContextSourceVersion subject) => subject.Value;
    [Fact]
    public void Constructor_WhenTextNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new ContextSourceVersion(null!)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenTextEmptyOrWhitespace_ThrowsArgumentException()
    {
        foreach (var value in new[]
        {
            string.Empty,
            " ",
            "\t"
        }

        )
        {
            var exception = Should.Throw<ArgumentException>(() => new ContextSourceVersion(value));
            _ = exception.ShouldBeOfType<ArgumentException>();
            exception.ParamName.ShouldBe("value");
        }
    }

    [Fact]
    public void Constructor_WhenTextValid_PreservesExactOrdinalValueEqualityAndDefaultFormatting()
    {
        var upper = new ContextSourceVersion("Source-A");
        var same = new ContextSourceVersion("Source-A");
        upper.Value.ShouldBe("Source-A");
        upper.ShouldBe(same);
        upper.GetHashCode().ShouldBe(same.GetHashCode());
        upper.ShouldNotBe(new ContextSourceVersion("source-a"));
        upper.ToString().ShouldBe("Source-A");
        default(ContextSourceVersion).ToString().ShouldBe(string.Empty);
    }
}
