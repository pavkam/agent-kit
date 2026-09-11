// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies IdentityNormalizationPolicyBinding behavior and contracts.</summary>
public sealed class IdentityNormalizationPolicyBindingTests
{
    [Fact]
    public void RuntimeBindings_WhenDependencyIsNull_ThrowWithParameterName()
    {
        var policyRegistration = new IdentityNormalizationPolicyRegistration("policy");
        var policy = new NullPolicy();
        Should.Throw<ArgumentNullException>(() => new IdentityNormalizationPolicyBinding(null!, policyRegistration)).ParamName.ShouldBe("policy");
        Should.Throw<ArgumentNullException>(() => new IdentityNormalizationPolicyBinding(policy, null!)).ParamName.ShouldBe("registration");
    }
}
