// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that a persisted consumed-intent receipt reproduces its identities, enforcement evidence, and optional fence.</summary>
/// <remarks>
/// The receipt is the durable half of grant consumption: it proves a use was spent and permission to start one exact effect
/// was retained. It never proves the effect began, so reconstruction must restore evidence without inventing ownership. An
/// invented fence would claim distributed ownership the grant store never issued.
/// </remarks>
public sealed class JsonSecurityEnforcementIntentReceiptTests
{
    /// <summary>Verifies a null receipt is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => JsonSecurityEnforcementIntentReceipt.FromDomain(null!));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a fenced receipt reconstructs as an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualReceipt()
    {
        var original = TestEvidenceFactory.Receipt(withFence: true);

        JsonSecurityEnforcementIntentReceipt.FromDomain(original).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the receipt survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEqualReceipt()
    {
        var original = TestEvidenceFactory.Receipt(withFence: true);

        var document = TestCanonicalJson.Cycle(JsonSecurityEnforcementIntentReceipt.FromDomain(original));

        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies a local effect's receipt stays unfenced rather than acquiring a fabricated ownership fence.</summary>
    [Fact]
    public void ToDomain_WhenEffectRequiredNoFence_LeavesRequiredFenceNull()
    {
        var original = TestEvidenceFactory.Receipt(withFence: false);
        original.RequiredFence.ShouldBeNull();

        var document = JsonSecurityEnforcementIntentReceipt.FromDomain(original);
        document.RequiredFence.ShouldBeNull();

        var restored = TestCanonicalJson.Cycle(document).ToDomain();
        restored.RequiredFence.ShouldBeNull();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies a required fence is preserved exactly rather than normalized to a different ownership epoch.</summary>
    [Fact]
    public void ToDomain_WhenEffectRequiredAFence_RetainsExactFence()
    {
        var original = TestEvidenceFactory.Receipt(withFence: true);

        var restored = TestCanonicalJson
            .Cycle(JsonSecurityEnforcementIntentReceipt.FromDomain(original))
            .ToDomain();

        restored.RequiredFence.ShouldBe(original.RequiredFence);
    }

    /// <summary>Verifies the nested enforcement evidence is delegated intact rather than flattened into the receipt.</summary>
    [Fact]
    public void ToDomain_WhenReceiptCarriesEnforcementEvidence_PreservesItExactly()
    {
        var original = TestEvidenceFactory.Receipt(withFence: true);

        var restored = TestCanonicalJson
            .Cycle(JsonSecurityEnforcementIntentReceipt.FromDomain(original))
            .ToDomain();

        restored.Enforcement.ShouldBe(original.Enforcement);
        restored.Enforcement.Resources.Select(static resource => resource.Identifier)
            .ShouldBe(original.Enforcement.Resources.Select(static resource => resource.Identifier));
    }

    /// <summary>Verifies missing enforcement evidence is refused rather than dereferenced.</summary>
    [Fact]
    public void ToDomain_WhenEnforcementIsNull_ThrowsArgumentNullException()
    {
        var source = JsonSecurityEnforcementIntentReceipt.FromDomain(TestEvidenceFactory.Receipt(withFence: true));
        var document = source with { Enforcement = null! };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Enforcement");
    }

    /// <summary>Verifies an empty persisted intent identity is rejected instead of rebuilt as a default identity.</summary>
    [Fact]
    public void ToDomain_WhenIntentIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityEnforcementIntentReceipt.FromDomain(TestEvidenceFactory.Receipt(withFence: false));
        var document = source with { IntentId = Guid.Empty };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies an empty persisted grant identity is rejected, since the spent use must be attributable.</summary>
    [Fact]
    public void ToDomain_WhenGrantIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityEnforcementIntentReceipt.FromDomain(TestEvidenceFactory.Receipt(withFence: false));
        var document = source with { GrantId = Guid.Empty };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies an empty persisted request identity is rejected, since the causal request must be attributable.</summary>
    [Fact]
    public void ToDomain_WhenRequestIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityEnforcementIntentReceipt.FromDomain(TestEvidenceFactory.Receipt(withFence: false));
        var document = source with { RequestId = Guid.Empty };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a non-positive persisted fence is rejected at the boundary just below the first valid token.</summary>
    [Fact]
    public void ToDomain_WhenRequiredFenceIsZero_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityEnforcementIntentReceipt.FromDomain(TestEvidenceFactory.Receipt(withFence: true));
        var document = source with { RequiredFence = 0 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted effect fingerprint is rejected rather than presented as valid consumption proof.</summary>
    [Fact]
    public void ToDomain_WhenEffectFingerprintIsBlank_ThrowsArgumentException()
    {
        var source = JsonSecurityEnforcementIntentReceipt.FromDomain(TestEvidenceFactory.Receipt(withFence: false));
        var document = source with { EffectFingerprint = "  " };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies the injected-clock commit instant survives unchanged, since reconciliation orders by it.</summary>
    [Fact]
    public void ToDomain_WhenConsumedAtIsPersisted_ReproducesExactInstant()
    {
        var original = TestEvidenceFactory.Receipt(withFence: false);

        var restored = TestCanonicalJson
            .Cycle(JsonSecurityEnforcementIntentReceipt.FromDomain(original))
            .ToDomain();

        restored.ConsumedAt.ShouldBe(original.ConsumedAt);
    }

    /// <summary>Verifies two documents decoded from byte-identical JSON compare equal by nested structure.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonSecurityEnforcementIntentReceipt.FromDomain(TestEvidenceFactory.Receipt(withFence: true));

        var first = TestCanonicalJson.Cycle(document);
        var second = TestCanonicalJson.Cycle(document);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Verifies an absent fence alone changes equality, since ownership evidence is materially different.</summary>
    [Fact]
    public void Equals_WhenOneReceiptIsFenced_ReturnsFalse()
    {
        var fenced = JsonSecurityEnforcementIntentReceipt.FromDomain(TestEvidenceFactory.Receipt(withFence: true));
        var local = JsonSecurityEnforcementIntentReceipt.FromDomain(TestEvidenceFactory.Receipt(withFence: false));

        fenced.Equals(local).ShouldBeFalse();
    }
}
