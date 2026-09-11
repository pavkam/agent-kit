// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityEnforcementBinding behavior and contracts.</summary>
public sealed class SecurityEnforcementBindingTests
{
    [Fact]
    public void Fingerprint_WhenIntentIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => SecurityEnforcementBinding.Fingerprint(Enforcement(), null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("intent");
    }

    [Fact]
    public void Fingerprint_WhenResourceTextHasDistinctUtf16CodeUnits_ProducesDistinctBindings()
    {
        string[] resourceValues = ["resource:\ud800", "resource:\ud801", "resource:\udc00", "resource:\udc01", "resource:\ufffd", "resource:\ud83d\ude00",];
        var fingerprints = resourceValues.Select(static value => SecurityEnforcementBinding.Fingerprint(Enforcement(value), new SecurityEnforcementIntent(IntentId(), null))).ToArray();
        fingerprints.Distinct().Count().ShouldBe(resourceValues.Length);
    }

    private static SecurityEnforcementRequest Enforcement(string resourceValue = "session:test")
    {
        var scope = new SecurityAuthorizationScope(new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")), null));
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SecurityEnforcementRequest(scope, identity, new ComponentId("session"), SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [new ProtectedResource(ProtectedResourceKind.ApplicationState, resourceValue)], new InputFingerprint("sha256:input"), new SecurityRevocationVersion(1));
    }

    private static SecurityEnforcementIntentId IntentId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
}
