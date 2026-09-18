// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that manifest evidence rejects every invalid component and exposes exactly what it was given.</summary>
/// <remarks>
/// The manifest is the value a later open compares against before decoding any record, so a manifest that accepted an empty
/// identity, a blank family name, or a blank fingerprint would let an incompatible root be reopened silently. Each guard is
/// therefore asserted with its exact parameter name.
/// </remarks>
public sealed class JsonStoreManifestTests
{
    private const string _storeKind = "agentkit.permissions.grants";
    private const string _formatFingerprint = "0123456789abcdef";

    /// <summary>Verifies an empty store identity is rejected, since it would compare equal to every corrupted manifest.</summary>
    [Fact]
    public void Constructor_WhenStoreIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonStoreManifest(Guid.Empty, _storeKind, 1, _formatFingerprint));

        exception.ParamName.ShouldBe("storeId");
    }

    /// <summary>Verifies a null storage-family discriminator is rejected before any member is assigned.</summary>
    [Fact]
    public void Constructor_WhenStoreKindIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new JsonStoreManifest(Guid.NewGuid(), null!, 1, _formatFingerprint));

        exception.ParamName.ShouldBe("storeKind");
    }

    /// <summary>Verifies a blank storage-family discriminator is rejected, so one root cannot be reused across families.</summary>
    [Fact]
    public void Constructor_WhenStoreKindIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new JsonStoreManifest(Guid.NewGuid(), "   ", 1, _formatFingerprint));

        exception.ParamName.ShouldBe("storeKind");
    }

    /// <summary>Verifies a zero schema version is rejected at the boundary just below the first valid layout.</summary>
    [Fact]
    public void Constructor_WhenSchemaVersionIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonStoreManifest(Guid.NewGuid(), _storeKind, 0, _formatFingerprint));

        exception.ParamName.ShouldBe("schemaVersion");
    }

    /// <summary>Verifies a negative schema version is rejected rather than treated as an unknown future layout.</summary>
    [Fact]
    public void Constructor_WhenSchemaVersionIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonStoreManifest(Guid.NewGuid(), _storeKind, -1, _formatFingerprint));

        exception.ParamName.ShouldBe("schemaVersion");
    }

    /// <summary>Verifies a null encoding fingerprint is rejected before any member is assigned.</summary>
    [Fact]
    public void Constructor_WhenFormatFingerprintIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new JsonStoreManifest(Guid.NewGuid(), _storeKind, 1, null!));

        exception.ParamName.ShouldBe("formatFingerprint");
    }

    /// <summary>Verifies a blank encoding fingerprint is rejected, since it could not detect a contract change.</summary>
    [Fact]
    public void Constructor_WhenFormatFingerprintIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new JsonStoreManifest(Guid.NewGuid(), _storeKind, 1, "\t"));

        exception.ParamName.ShouldBe("formatFingerprint");
    }

    /// <summary>Verifies a valid manifest exposes exactly the identity, family, version, and fingerprint it captured.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedEvidence()
    {
        var storeId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        var manifest = new JsonStoreManifest(storeId, _storeKind, 3, _formatFingerprint);

        manifest.StoreId.ShouldBe(storeId);
        manifest.StoreKind.ShouldBe(_storeKind);
        manifest.SchemaVersion.ShouldBe(3);
        manifest.FormatFingerprint.ShouldBe(_formatFingerprint);
    }

    /// <summary>Verifies two manifests describing the same root compare equal, which is what an open-time check relies on.</summary>
    [Fact]
    public void Equals_WhenEveryComponentMatches_ReturnsTrue()
    {
        var storeId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        var first = new JsonStoreManifest(storeId, _storeKind, 3, _formatFingerprint);
        var second = new JsonStoreManifest(storeId, _storeKind, 3, _formatFingerprint);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Verifies a differing encoding fingerprint breaks equality, so a contract change fails the open check.</summary>
    [Fact]
    public void Equals_WhenFormatFingerprintDiffers_ReturnsFalse()
    {
        var storeId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        var first = new JsonStoreManifest(storeId, _storeKind, 3, _formatFingerprint);
        var second = new JsonStoreManifest(storeId, _storeKind, 3, "fedcba9876543210");

        first.ShouldNotBe(second);
    }

    /// <summary>Verifies the manifest survives the canonical encoding it is itself persisted under.</summary>
    [Fact]
    public void Constructor_WhenPersistedUnderCanonicalContract_RoundTripsUnchanged()
    {
        var manifest = new JsonStoreManifest(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), _storeKind, 3, _formatFingerprint);

        TestCanonicalJson.Cycle(manifest).ShouldBe(manifest);
    }
}
