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

    [Fact]
    public void Properties_WhenConstructed_ExposeTheExactCapturedValues()
    {
        var registration = new IdentityNormalizationPolicyRegistration("policy", 3);
        var policy = new NullPolicy();

        var binding = new IdentityNormalizationPolicyBinding(policy, registration);

        binding.Policy.ShouldBeSameAs(policy);
        binding.Registration.ShouldBeSameAs(registration);
        binding.ToString().ShouldContain(nameof(IdentityNormalizationPolicyBinding));
        (binding with { }).ShouldBe(binding);
    }

    [Fact]
    public void Equality_WhenNameAndOrderMatch_TreatsRegistrationsAsEqual()
    {
        var first = new IdentityNormalizationPolicyRegistration("policy", 3);
        var second = new IdentityNormalizationPolicyRegistration("policy", 3);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.Name.ShouldBe("policy");
        first.Order.ShouldBe(3);
        first.ToString().ShouldContain(nameof(IdentityNormalizationPolicyRegistration));
        (first with { }).ShouldBe(first);
    }
}
