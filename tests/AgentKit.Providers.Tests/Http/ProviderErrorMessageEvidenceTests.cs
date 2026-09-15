// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Http;

using System.Text;
using System.Text.Json;

using AgentKit.Providers.Http;

/// <summary>Verifies that provider error text is retained as bounded diagnostic evidence and read back exactly.</summary>
public sealed class ProviderErrorMessageEvidenceTests
{
    [Fact]
    public void Key_WhenRead_IsStableAgentKitNamespacedName() =>
        ProviderErrorMessageEvidence.Key.ShouldBe("agentkit.provider.error_message");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Create_WhenMessageIsMissingOrBlank_ReturnsEmptyExtensionData(string? providerMessage)
    {
        var extensions = ProviderErrorMessageEvidence.Create(providerMessage);

        extensions.ShouldBeSameAs(ExtensionData.Empty);
        extensions.Values.ShouldBeEmpty();
    }

    [Fact]
    public void Create_WhenMessageIsPresent_StoresOneJsonStringEntryUnderKey()
    {
        var extensions = ProviderErrorMessageEvidence.Create("invalid api token");

        var entry = extensions.Values.ShouldHaveSingleItem();
        entry.Key.ShouldBe(ProviderErrorMessageEvidence.Key);
        Encoding.UTF8.GetString(entry.Value.CanonicalJson.AsSpan()).ShouldBe("\"invalid api token\"");
    }

    [Fact]
    public void Create_WhenMessageHasSurroundingWhitespace_TrimsIt()
    {
        var extensions = ProviderErrorMessageEvidence.Create("  Unauthorized \n");

        ProviderErrorMessageEvidence.TryRead(extensions).ShouldBe("Unauthorized");
    }

    [Fact]
    public void Create_WhenMessageIsExactlyMaxLength_RetainsItUnchanged()
    {
        var message = new string('x', ProviderErrorMessageEvidence.MaxLength);

        var extensions = ProviderErrorMessageEvidence.Create(message);

        ProviderErrorMessageEvidence.TryRead(extensions).ShouldBe(message);
    }

    [Fact]
    public void Create_WhenMessageExceedsMaxLength_TruncatesToMaxLength()
    {
        var message = new string('x', ProviderErrorMessageEvidence.MaxLength) + "OVERFLOW";

        var extensions = ProviderErrorMessageEvidence.Create(message);

        var retained = ProviderErrorMessageEvidence.TryRead(extensions).ShouldNotBeNull();
        retained.Length.ShouldBe(ProviderErrorMessageEvidence.MaxLength);
        retained.ShouldNotContain("OVERFLOW");
    }

    [Fact]
    public void Create_WhenMessageContainsJsonSignificantCharacters_RoundTripsExactly()
    {
        const string message = "Authorization failed for \"sk-live\" <script>alert(1)</script> \\ tenant alice@example.test \u00e9";

        var extensions = ProviderErrorMessageEvidence.Create(message);

        ProviderErrorMessageEvidence.TryRead(extensions).ShouldBe(message);
    }

    [Fact]
    public void Create_WhenCalledTwiceWithSameMessage_ProducesEqualExtensionData()
    {
        var first = ProviderErrorMessageEvidence.Create("throttled");
        var second = ProviderErrorMessageEvidence.Create("throttled");

        first.ShouldBe(second);
    }

    [Fact]
    public void TryRead_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ProviderErrorMessageEvidence.TryRead(null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void TryRead_WhenKeyIsAbsent_ReturnsNull()
    {
        var extensions = new ExtensionData(
            ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                "unrelated",
                new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("value")])));

        ProviderErrorMessageEvidence.TryRead(extensions).ShouldBeNull();
    }

    [Fact]
    public void TryRead_WhenEmpty_ReturnsNull() =>
        ProviderErrorMessageEvidence.TryRead(ExtensionData.Empty).ShouldBeNull();

    [Fact]
    public void TryRead_WhenEntryIsNotJsonString_ReturnsNull()
    {
        var extensions = new ExtensionData(
            ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                ProviderErrorMessageEvidence.Key,
                new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(new { message = "nested" })])));

        ProviderErrorMessageEvidence.TryRead(extensions).ShouldBeNull();
    }
}
