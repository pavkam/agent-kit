// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ContextSourceKey behavior and contracts.</summary>
public sealed class ContextSourceKeyTests: Conformance.StringIdentityConformanceTests<ContextSourceKey>
{
    /// <inheritdoc/>
    protected override ContextSourceKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ContextSourceKey subject) => subject.Value;
    [Fact]
    public void Constructor_WhenTextNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new ContextSourceKey(null!)).ParamName.ShouldBe("value");

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
            var exception = Should.Throw<ArgumentException>(() => new ContextSourceKey(value));
            _ = exception.ShouldBeOfType<ArgumentException>();
            exception.ParamName.ShouldBe("value");
        }
    }

    [Fact]
    public void Constructor_WhenTextValid_PreservesExactOrdinalValueEqualityAndDefaultFormatting()
    {
        var upper = new ContextSourceKey("Source-A");
        var same = new ContextSourceKey("Source-A");
        upper.Value.ShouldBe("Source-A");
        upper.ShouldBe(same);
        upper.GetHashCode().ShouldBe(same.GetHashCode());
        upper.ShouldNotBe(new ContextSourceKey("source-a"));
        upper.ToString().ShouldBe("Source-A");
        default(ContextSourceKey).ToString().ShouldBe(string.Empty);
    }
}
