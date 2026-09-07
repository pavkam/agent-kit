// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

public sealed class IdentityResultContractsTests
{
    [Fact]
    public void IdentityFailure_WhenMessageIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityFailure(IdentityFailureKind.Malformed, " "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void IdentityRejected_WhenCreated_PreservesTypedFailure()
    {
        var failure = new IdentityFailure(IdentityFailureKind.Expired, "Authentication evidence expired.", new IdentityIssuerId("issuer"));

        var result = new IdentityRejected(failure);

        result.Failure.ShouldBeSameAs(failure);
    }

    [Fact]
    public void IdentityValidationPassed_Instance_IsShared() =>
        IdentityValidationPassed.Instance.ShouldBeSameAs(IdentityValidationPassed.Instance);
}
