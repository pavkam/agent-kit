// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies ReasoningContent behavior and contracts.</summary>
public sealed class ReasoningContentTests
{
    [Fact]
    public void ReasoningContent_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReasoningContent(null, ReasoningVisibility.Visible, null, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ReasoningContent_Equality_WhenSameValues_InstancesAreEqual()
    {
        var content = new ReasoningContent("thinking", ReasoningVisibility.Visible, null, ExtensionData.Empty);
        content.ShouldBe(new ReasoningContent("thinking", ReasoningVisibility.Visible, null, ExtensionData.Empty));
        content.Extensions.ShouldBe(ExtensionData.Empty);
    }
    [Fact]
    public void ReasoningContent_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReasoningContent("thinking", ReasoningVisibility.Visible, null, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ReasoningContent_WhenVisibilityIsRedacted_TextMayBeNull()
    {
        var content = new ReasoningContent(null, ReasoningVisibility.Redacted, null, ExtensionData.Empty);
        content.Text.ShouldBeNull();
        content.Visibility.ShouldBe(ReasoningVisibility.Redacted);
    }

    [Fact]
    public void With_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var content = new ReasoningContent("thinking", ReasoningVisibility.Visible, null, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => content with { Extensions = null! });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenVisibilityIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ReasoningContent(null, (ReasoningVisibility) 99, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("visibility");
    }

    [Theory]
    [InlineData(ReasoningVisibility.Redacted)]
    [InlineData(ReasoningVisibility.EncryptedSignature)]
    public void Constructor_WhenVisibilityHidesTextButTextIsSupplied_ThrowsArgumentException(ReasoningVisibility visibility)
    {
        var exception = Should.Throw<ArgumentException>(() => new ReasoningContent("leaked", visibility, "sig", ExtensionData.Empty));

        exception.ParamName.ShouldBe("text");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenEncryptedSignatureLacksToken_ThrowsArgumentException(string? signatureToken)
    {
        var exception = Should.Throw<ArgumentException>(() => new ReasoningContent(null, ReasoningVisibility.EncryptedSignature, signatureToken, ExtensionData.Empty));

        exception.ParamName.ShouldBe("signatureToken");
    }

    [Fact]
    public void Constructor_WhenEncryptedSignatureCarriesToken_Succeeds()
    {
        var content = new ReasoningContent(null, ReasoningVisibility.EncryptedSignature, "opaque", ExtensionData.Empty);

        content.Text.ShouldBeNull();
        content.SignatureToken.ShouldBe("opaque");
    }

    [Fact]
    public void Constructor_WhenVisibleTextIsNull_Succeeds()
    {
        var content = new ReasoningContent(null, ReasoningVisibility.Visible, null, ExtensionData.Empty);

        content.Text.ShouldBeNull();
        content.Visibility.ShouldBe(ReasoningVisibility.Visible);
    }
}
