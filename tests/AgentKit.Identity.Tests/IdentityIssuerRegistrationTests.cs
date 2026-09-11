// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies IdentityIssuerRegistration behavior and contracts.</summary>
public sealed class IdentityIssuerRegistrationTests
{
    [Fact]
    public void IdentityIssuerRegistration_WhenIssuerIsDefault_ThrowsWithIssuerIdParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityIssuerRegistration(default));
        exception.ParamName.ShouldBe("issuerId");
    }
}
