// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableJournalKey behavior and contracts.</summary>
public sealed class DurableJournalKeyTests: Conformance.StringIdentityConformanceTests<DurableJournalKey>
{
    [Fact]
    public void DurableJournalKey_Equality_WhenSameTextAsBackendKey_TypesRemainDistinct()
    {
        object journal = new DurableJournalKey("shared");
        object backend = new DurableBackendKey("shared");
        journal.ShouldNotBe(backend);
    }

    /// <inheritdoc/>
    protected override DurableJournalKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(DurableJournalKey subject) => subject.Value;
    [Fact]
    public void TextKey_Constructor_WhenValueIsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableJournalKey(null!));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableJournalKey(string.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableJournalKey("   "));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_ToString_ReturnsCanonicalText() => new DurableJournalKey("canonical").ToString().ShouldBe("canonical");
}
