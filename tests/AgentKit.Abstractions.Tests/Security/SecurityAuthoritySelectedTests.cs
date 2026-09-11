// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

using AgentKit.TestSupport;

public sealed class SecurityAuthoritySelectedTests
{
    [Fact]
    public void Constructor_WhenAuthorizationOrAuthorityIsNull_ThrowsWithExactParameterNames()
    {
        var context = TestSecurityEvidence.Authorization(RunResultTestData.Agent, RunResultTestData.Session,
            new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("d4444444-4444-4444-4444-444444444444")), null),
            TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human));
        var invalidContext = Should.Throw<ArgumentNullException>(() => new SecurityAuthoritySelected(null!, new UninvokedSecurityAuthority()));
        var invalidAuthority = Should.Throw<ArgumentNullException>(() => new SecurityAuthoritySelected(context, null!));
        invalidContext.GetType().ShouldBe(typeof(ArgumentNullException));
        invalidContext.ParamName.ShouldBe("authorization");
        invalidAuthority.GetType().ShouldBe(typeof(ArgumentNullException));
        invalidAuthority.ParamName.ShouldBe("authority");
    }
}
