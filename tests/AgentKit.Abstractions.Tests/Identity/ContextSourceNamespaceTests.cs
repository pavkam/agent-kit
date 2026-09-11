// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ContextSourceNamespace behavior and contracts.</summary>
public sealed class ContextSourceNamespaceTests: Conformance.StringIdentityConformanceTests<ContextSourceNamespace>
{
    /// <inheritdoc/>
    protected override ContextSourceNamespace Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ContextSourceNamespace subject) => subject.Value;
    [Fact]
    public void Constructor_WhenTextNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new ContextSourceNamespace(null!)).ParamName.ShouldBe("value");

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
            var exception = Should.Throw<ArgumentException>(() => new ContextSourceNamespace(value));
            _ = exception.ShouldBeOfType<ArgumentException>();
            exception.ParamName.ShouldBe("value");
        }
    }

    [Fact]
    public void Constructor_WhenTextValid_PreservesExactOrdinalValueEqualityAndDefaultFormatting()
    {
        var upper = new ContextSourceNamespace("Source-A");
        var same = new ContextSourceNamespace("Source-A");
        upper.Value.ShouldBe("Source-A");
        upper.ShouldBe(same);
        upper.GetHashCode().ShouldBe(same.GetHashCode());
        upper.ShouldNotBe(new ContextSourceNamespace("source-a"));
        upper.ToString().ShouldBe("Source-A");
        default(ContextSourceNamespace).ToString().ShouldBe(string.Empty);
    }
}
