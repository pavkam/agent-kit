// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityEnforcementFingerprintPayload behavior and contracts.</summary>
public sealed class SecurityEnforcementFingerprintPayloadTests
{
    [Fact]
    public void Constructor_WhenEnforcementIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SecurityEnforcementFingerprintPayload(null!, Intent())).ParamName.ShouldBe("enforcement");

    [Fact]
    public void Constructor_WhenIntentIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SecurityEnforcementFingerprintPayload(Enforcement(), null!)).ParamName.ShouldBe("intent");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var enforcement = Enforcement();
        var intent = Intent();
        var payload = new SecurityEnforcementFingerprintPayload(enforcement, intent);
        payload.Enforcement.ShouldBe(enforcement);
        payload.Intent.ShouldBe(intent);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityEnforcementFingerprintPayload(Enforcement(), Intent());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SecurityEnforcementRequest Enforcement() =>
        new(SecurityAbstractionsTestData.Scope(), SecurityAbstractionsTestData.Identity(), new ComponentId("session"),
            SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [SecurityAbstractionsTestData.Resource()],
            new InputFingerprint("sha256:input"), new SecurityRevocationVersion(1));

    private static SecurityEnforcementIntent Intent() =>
        new(new SecurityEnforcementIntentId(Guid.Parse("f0000000-0000-0000-0000-00000000000b")), null);
}
