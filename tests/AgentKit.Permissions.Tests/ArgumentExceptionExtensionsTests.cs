// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies package-owned security composition argument guards.</summary>
public sealed class ArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfDuplicateSecurityAuthorityBinding_WhenSnapshotIsValidOrEmpty_DoesNotThrow()
    {
        IReadOnlyList<SecurityAuthorityBinding> empty = [];
        IReadOnlyList<SecurityAuthorityBinding> one = [new(new ComponentKey<ISecurityAuthority>("security.primary"), new UninvokedSecurityAuthority())];
        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateSecurityAuthorityBinding(empty));
        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateSecurityAuthorityBinding(one));
    }

    [Fact]
    public void ThrowIfDuplicateSecurityAuthorityBinding_WhenSnapshotHasDuplicate_ReportsInferredAndExplicitParameterNames()
    {
        IReadOnlyList<SecurityAuthorityBinding> bindings = [new(new ComponentKey<ISecurityAuthority>("security.primary"), new UninvokedSecurityAuthority()), new(new ComponentKey<ISecurityAuthority>("security.primary"), new UninvokedSecurityAuthority()),];
        var inferred = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateSecurityAuthorityBinding(bindings));
        var explicitName = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateSecurityAuthorityBinding(bindings, "authorityBindings"));
        inferred.ParamName.ShouldBe(nameof(bindings));
        explicitName.ParamName.ShouldBe("authorityBindings");
    }

    [Fact]
    public void ThrowIfDuplicateSecurityAuthorityBinding_WhenSnapshotOrEntryIsNull_ThrowsArgumentNullException()
    {
        var nullSnapshot = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfDuplicateSecurityAuthorityBinding(null!));
        IReadOnlyList<SecurityAuthorityBinding> nullEntry = [null!];
        var nullBinding = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfDuplicateSecurityAuthorityBinding(nullEntry));
        nullSnapshot.GetType().ShouldBe(typeof(ArgumentNullException));
        nullSnapshot.ParamName.ShouldBe("null");
        nullBinding.GetType().ShouldBe(typeof(ArgumentNullException));
        nullBinding.ParamName.ShouldBe(nameof(nullEntry));
    }

}
