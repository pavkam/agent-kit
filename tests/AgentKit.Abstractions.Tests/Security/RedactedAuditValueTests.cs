// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies RedactedAuditValue behavior and contracts.</summary>
public sealed class RedactedAuditValueTests
{
    [Fact]
    public void FromFingerprint_WhenGivenSourceText_ComputesDigestWithoutRetainingTheSourceText()
    {
        const string source = "raw-secret-marker";
        var value = RedactedAuditValue.FromFingerprint(new ContentHash(source));
        value.Kind.ShouldBe(SecurityAuditValueKind.Fingerprint);
        value.Value.ShouldStartWith("sha256:");
        value.Value.Length.ShouldBe(71);
        value.Value.ShouldNotContain(source);
    }

    [Fact]
    public void FromFingerprint_WhenFingerprintIsDefault_ThrowsArgumentNullExceptionForFingerprint()
    {
        var exception = Should.Throw<ArgumentNullException>(() => RedactedAuditValue.FromFingerprint(default));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("fingerprint");
    }

    [Fact]
    public void AuditResultAndRedactedValueFactories_WhenArgumentsAreInvalid_ThrowWithExactParameterNames()
    {
        Should.Throw<ArgumentNullException>(() => RedactedAuditValue.FromPolicyId(default)).ParamName.ShouldBe("policyId");
        Should.Throw<ArgumentNullException>(() => RedactedAuditValue.FromComponentId(default)).ParamName.ShouldBe("componentId");
        Should.Throw<ArgumentOutOfRangeException>(() => RedactedAuditValue.FromOperationKind((SecurityOperationKind) 99)).ParamName.ShouldBe("kind");
        Should.Throw<ArgumentOutOfRangeException>(() => RedactedAuditValue.FromEffect((SecurityEffect) 99)).ParamName.ShouldBe("effect");
    }

    [Fact]
    public void FromPolicyId_WhenValid_RetainsTypedFact()
    {
        var value = RedactedAuditValue.FromPolicyId(new SecurityPolicyId("policy"));
        value.Kind.ShouldBe(SecurityAuditValueKind.SecurityPolicyId);
        value.Value.ShouldBe("policy");
    }

    [Fact]
    public void FromComponentId_WhenValid_RetainsTypedFact()
    {
        var value = RedactedAuditValue.FromComponentId(new ComponentId("component"));
        value.Kind.ShouldBe(SecurityAuditValueKind.ComponentId);
        value.Value.ShouldBe("component");
    }

    [Fact]
    public void FromOperationKind_WhenValid_RetainsEnumName()
    {
        var value = RedactedAuditValue.FromOperationKind(SecurityOperationKind.StateRead);
        value.Kind.ShouldBe(SecurityAuditValueKind.OperationKind);
        value.Value.ShouldBe(nameof(SecurityOperationKind.StateRead));
    }

    [Fact]
    public void FromEffect_WhenValid_RetainsEnumName()
    {
        var value = RedactedAuditValue.FromEffect(SecurityEffect.Observe);
        value.Kind.ShouldBe(SecurityAuditValueKind.Effect);
        value.Value.ShouldBe(nameof(SecurityEffect.Observe));
    }
}
