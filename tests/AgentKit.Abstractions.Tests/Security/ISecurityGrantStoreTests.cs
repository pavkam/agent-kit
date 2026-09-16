// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies ISecurityGrantStore default behavior and contracts.</summary>
public sealed class ISecurityGrantStoreTests
{
    [Fact]
    public void ValidateAndConsumeAsync_WithIntent_WhenReferenceIsNull_ThrowsExactArgumentNullException()
    {
        ISecurityGrantStore store = new UnsupportedGrantStore();
        var grant = SecurityAbstractionsTestData.Grant();
        var enforcement = Enforcement();
        var intent = Intent();
        Should.Throw<ArgumentNullException>(() => _ = store.ValidateAndConsumeAsync(null!, enforcement, intent).AsTask()).ParamName.ShouldBe("grant");
        Should.Throw<ArgumentNullException>(() => _ = store.ValidateAndConsumeAsync(grant, null!, intent).AsTask()).ParamName.ShouldBe("enforcement");
        Should.Throw<ArgumentNullException>(() => _ = store.ValidateAndConsumeAsync(grant, enforcement, null!).AsTask()).ParamName.ShouldBe("intent");
    }

    [Fact]
    public void ValidateAndConsumeAsync_WithIntent_WhenCallerAlreadyCancelled_PreservesCancellationToken()
    {
        ISecurityGrantStore store = new UnsupportedGrantStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Should.Throw<OperationCanceledException>(() => _ = store.ValidateAndConsumeAsync(SecurityAbstractionsTestData.Grant(), Enforcement(), Intent(), cancellation.Token).AsTask())
            .CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithIntent_WhenNotOverridden_ReturnsUnknownResult()
    {
        ISecurityGrantStore store = new UnsupportedGrantStore();
        var result = await store.ValidateAndConsumeAsync(SecurityAbstractionsTestData.Grant(), Enforcement(), Intent(), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GrantConsumptionStatus.Unknown);
        result.IntentReceipt.ShouldBeNull();
    }

    private static SecurityEnforcementRequest Enforcement() =>
        new(SecurityAbstractionsTestData.Scope(), SecurityAbstractionsTestData.Identity(), new ComponentId("session"),
            SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [SecurityAbstractionsTestData.Resource()],
            new InputFingerprint("sha256:input"), new SecurityRevocationVersion(1));

    private static SecurityEnforcementIntent Intent() =>
        new(new SecurityEnforcementIntentId(Guid.Parse("f0000000-0000-0000-0000-00000000000a")), null);

    private sealed class UnsupportedGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
