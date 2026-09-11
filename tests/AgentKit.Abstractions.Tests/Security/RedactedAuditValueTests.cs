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
}
