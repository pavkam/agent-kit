// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that an encoding contract is frozen at construction and that records are always written on one line.</summary>
/// <remarks>
/// Two guarantees matter here. Freezing the supplied options prevents a caller that kept a reference from changing the format
/// after composition, and deriving a compact record contract keeps newline-delimited framing intact no matter how the caller
/// configured indentation.
/// </remarks>
public sealed class JsonEncodingSettingsTests
{
    /// <summary>Verifies null options are rejected instead of producing a contract with no fingerprint.</summary>
    [Fact]
    public void Constructor_WhenSerializerOptionsAreNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new JsonEncodingSettings(null!));
        exception.ParamName.ShouldBe("serializerOptions");
    }

    /// <summary>Verifies the supplied instance is frozen, so a retained reference cannot mutate the contract afterwards.</summary>
    [Fact]
    public void Constructor_WhenCalled_MakesSuppliedOptionsReadOnly()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var settings = new JsonEncodingSettings(options);

        options.IsReadOnly.ShouldBeTrue();
        settings.DocumentOptions.ShouldBeSameAs(options);
        _ = Should.Throw<InvalidOperationException>(() => options.WriteIndented = true);
    }

    /// <summary>Verifies an unindented contract reuses one frozen instance for both documents and records.</summary>
    [Fact]
    public void RecordOptions_WhenSourceIsNotIndented_ReturnsSameInstanceAsDocumentOptions()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var settings = new JsonEncodingSettings(options);

        settings.RecordOptions.ShouldBeSameAs(settings.DocumentOptions);
        settings.RecordOptions.WriteIndented.ShouldBeFalse();
    }

    /// <summary>Verifies an indented contract derives a separate frozen compact contract for record framing.</summary>
    [Fact]
    public void RecordOptions_WhenSourceIsIndented_ReturnsFrozenCompactContract()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();
        options.WriteIndented = true;

        var settings = new JsonEncodingSettings(options);

        settings.RecordOptions.ShouldNotBeSameAs(settings.DocumentOptions);
        settings.RecordOptions.WriteIndented.ShouldBeFalse();
        settings.RecordOptions.IsReadOnly.ShouldBeTrue();
    }

    /// <summary>Verifies a record written under an indented contract still occupies exactly one line.</summary>
    [Fact]
    public void RecordOptions_WhenSourceIsIndented_WritesSingleLineRecords()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();
        options.WriteIndented = true;
        var settings = new JsonEncodingSettings(options);

        var record = JsonSerializer.Serialize(
            new JsonProtectedResource(ProtectedResourceKind.File, "/workspace/a.txt"),
            settings.RecordOptions);

        record.ShouldNotContain("\n");
    }

    /// <summary>Verifies document rewrites honor the caller's requested indentation.</summary>
    [Fact]
    public void DocumentOptions_WhenSourceIsIndented_PreservesIndentation()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();
        options.WriteIndented = true;
        var settings = new JsonEncodingSettings(options);

        settings.DocumentOptions.WriteIndented.ShouldBeTrue();
        JsonSerializer.Serialize(
            new JsonProtectedResource(ProtectedResourceKind.File, "/workspace/a.txt"),
            settings.DocumentOptions).ShouldContain("\n");
    }

    /// <summary>Verifies the retained fingerprint is exactly the one computed for the effective option set.</summary>
    [Fact]
    public void Fingerprint_WhenConstructed_MatchesComputedFingerprint()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var settings = new JsonEncodingSettings(options);

        settings.Fingerprint.ShouldBe(JsonFormatFingerprint.Compute(options));
    }

    /// <summary>Verifies two contracts with the same semantic fingerprint compare equal, since either can read the other's records.</summary>
    [Fact]
    public void Equals_WhenFingerprintsMatch_ReturnsTrue()
    {
        var first = new JsonEncodingSettings(JsonStoreSerialization.CreateCanonicalOptions());
        var second = new JsonEncodingSettings(JsonStoreSerialization.CreateCanonicalOptions());

        first.ShouldBe(second);
    }

    /// <summary>Verifies an indentation-only difference does not break equality, because framing is unaffected.</summary>
    [Fact]
    public void Equals_WhenOnlyIndentationDiffers_ReturnsTrue()
    {
        var indentedOptions = JsonStoreSerialization.CreateCanonicalOptions();
        indentedOptions.WriteIndented = true;

        var compact = new JsonEncodingSettings(JsonStoreSerialization.CreateCanonicalOptions());
        var indented = new JsonEncodingSettings(indentedOptions);

        compact.Equals(indented).ShouldBeTrue();
    }

    /// <summary>Verifies a semantic difference breaks equality, which is what makes a manifest mismatch detectable.</summary>
    [Fact]
    public void Equals_WhenFingerprintsDiffer_ReturnsFalse()
    {
        var changedOptions = JsonStoreSerialization.CreateCanonicalOptions();
        changedOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;

        var canonical = new JsonEncodingSettings(JsonStoreSerialization.CreateCanonicalOptions());
        var changed = new JsonEncodingSettings(changedOptions);

        canonical.Equals(changed).ShouldBeFalse();
    }

    /// <summary>Verifies a null candidate is never equal, so an absent contract cannot pass a compatibility check.</summary>
    [Fact]
    public void Equals_WhenOtherIsNull_ReturnsFalse()
    {
        var settings = JsonEncodingSettings.CreateDefault();

        settings.Equals(null).ShouldBeFalse();
    }

    /// <summary>Verifies the hash agrees with fingerprint equality so contracts can key a dictionary.</summary>
    [Fact]
    public void GetHashCode_WhenFingerprintsMatch_ReturnsSameValue()
    {
        var first = new JsonEncodingSettings(JsonStoreSerialization.CreateCanonicalOptions());
        var second = new JsonEncodingSettings(JsonStoreSerialization.CreateCanonicalOptions());

        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Verifies the default contract is exactly the canonical one this package is tested against.</summary>
    [Fact]
    public void CreateDefault_WhenCalled_UsesCanonicalContract()
    {
        var settings = JsonEncodingSettings.CreateDefault();

        settings.Fingerprint.ShouldBe(
            JsonFormatFingerprint.Compute(JsonStoreSerialization.CreateCanonicalOptions()));
        settings.DocumentOptions.IsReadOnly.ShouldBeTrue();
        settings.RecordOptions.WriteIndented.ShouldBeFalse();
    }
}
