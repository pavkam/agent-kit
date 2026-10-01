// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Observation;

/// <summary>Verifies <see cref="RedactionResult"/> and its canonical variants.</summary>
public sealed class RedactionResultTests
{
    [Fact]
    public void Constructor_WhenRedactedContentIsNull_ThrowsArgumentNullExceptionNamingContent() =>
        Should.Throw<ArgumentNullException>(() => new RedactedContent(null!)).ParamName.ShouldBe("content");

    [Fact]
    public void Constructor_WhenRedactedContentIsProvided_ExposesContent()
    {
        var content = new ObservationContent(ObservationContentKind.Prompt, DataClassification.Public, [1], new ContentFingerprint("f"));

        new RedactedContent(content).Content.ShouldBeSameAs(content);
    }

    [Fact]
    public void CopyConstructor_WhenExternalVariantCopiesBuiltIn_RejectsForeignVariant()
    {
        var exception = Should.Throw<ArgumentException>(() => new ForeignVariant(new ContentOmitted()));

        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void CopyConstructor_WhenOriginalIsNull_ThrowsArgumentNullExceptionNamingOriginal() =>
        Should.Throw<ArgumentNullException>(() => new ForeignVariant(null!)).ParamName.ShouldBe("original");

    [Fact]
    public void With_WhenAppliedToOmission_ProducesEqualCopyOfTheSameVariant()
    {
        RedactionResult original = new ContentOmitted();

        var copy = original with { };

        copy.ShouldBe(original);
        copy.GetType().ShouldBe(typeof(ContentOmitted));
    }

    private sealed record ForeignVariant(RedactionResult Original): RedactionResult(Original);
}
