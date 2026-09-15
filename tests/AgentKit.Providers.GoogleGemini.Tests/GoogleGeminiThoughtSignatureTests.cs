// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests;

/// <summary>Verifies <see cref="GoogleGeminiThoughtSignature"/> behavior and contracts.</summary>
public sealed class GoogleGeminiThoughtSignatureTests
{
    [Fact]
    public void Create_WhenSignatureSupplied_StoresJsonStringUnderStableKey()
    {
        var extensions = GoogleGeminiThoughtSignature.Create("sig_abc");

        var value = extensions.Values.ShouldHaveSingleItem();
        value.Key.ShouldBe(GoogleGeminiExtensionKeys.ThoughtSignature);
        value.Key.ShouldBe("gemini.thought_signature");
        Encoding.UTF8.GetString(value.Value.CanonicalJson.AsSpan()).ShouldBe("\"sig_abc\"");
    }

    [Fact]
    public void Create_WhenSignatureIsNull_ReturnsEmptyExtensions() =>
        GoogleGeminiThoughtSignature.Create(null).ShouldBeSameAs(ExtensionData.Empty);

    [Fact]
    public void Create_WhenSignatureIsEmpty_ReturnsEmptyExtensions() =>
        GoogleGeminiThoughtSignature.Create(string.Empty).ShouldBeSameAs(ExtensionData.Empty);

    [Fact]
    public void TryRead_WhenCreatedByCreate_RoundTripsSignatureExactly()
    {
        const string signature = "CqYBAVSoXO7+base64/looking==";

        GoogleGeminiThoughtSignature.TryRead(GoogleGeminiThoughtSignature.Create(signature)).ShouldBe(signature);
    }

    [Fact]
    public void TryRead_WhenKeyAbsent_ReturnsNull() =>
        GoogleGeminiThoughtSignature.TryRead(ExtensionData.Empty).ShouldBeNull();

    [Fact]
    public void TryRead_WhenValueIsNotJsonString_ReturnsNull()
    {
        var extensions = new ExtensionData(
            ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                GoogleGeminiExtensionKeys.ThoughtSignature,
                new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(42)])));

        GoogleGeminiThoughtSignature.TryRead(extensions).ShouldBeNull();
    }

    [Fact]
    public void TryRead_WhenValueIsEmptyJsonString_ReturnsNull()
    {
        var extensions = new ExtensionData(
            ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                GoogleGeminiExtensionKeys.ThoughtSignature,
                new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(string.Empty)])));

        GoogleGeminiThoughtSignature.TryRead(extensions).ShouldBeNull();
    }

    [Fact]
    public void TryRead_WhenOtherKeysPresent_IgnoresThem()
    {
        var extensions = new ExtensionData(
            ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                "unrelated",
                new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("value")])));

        GoogleGeminiThoughtSignature.TryRead(extensions).ShouldBeNull();
    }

    [Fact]
    public void TryRead_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => GoogleGeminiThoughtSignature.TryRead(null!));

        exception.ParamName.ShouldBe("extensions");
    }
}
