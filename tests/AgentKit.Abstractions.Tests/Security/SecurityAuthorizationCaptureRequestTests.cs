// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityAuthorizationCaptureRequest behavior and contracts.</summary>
public sealed class SecurityAuthorizationCaptureRequestTests
{
    /// <summary>Verifies a capture request rejects missing trusted facts rather than fabricating defaults.</summary>
    [Fact]
    public void Constructor_WhenCaptureRequestPartIsInvalid_ThrowsExactArgument()
    {
        Should.Throw<ArgumentNullException>(() => new SecurityAuthorizationCaptureRequest(null!, new SecurityProfileKey("default"), new AgentDefinitionRevision(0), new ConfigurationVersion(9), Identity())).ParamName.ShouldBe("scope");
        Should.Throw<ArgumentNullException>(() => new SecurityAuthorizationCaptureRequest(Scope(), default, new AgentDefinitionRevision(0), new ConfigurationVersion(9), Identity())).ParamName.ShouldBe("profileKey");
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuthorizationCaptureRequest(Scope(), new SecurityProfileKey("default"), new AgentDefinitionRevision(0), default, Identity())).ParamName.ShouldBe("configurationVersion");
        Should.Throw<ArgumentNullException>(() => new SecurityAuthorizationCaptureRequest(Scope(), new SecurityProfileKey("default"), new AgentDefinitionRevision(0), new ConfigurationVersion(9), null!)).ParamName.ShouldBe("identity");
    }

    /// <summary>Verifies snapshot aggregates have structural equality and get-only public state.</summary>
    [Fact]
    public void Equality_WhenCapturedValuesMatch_IsStructuralAndImmutable()
    {
        Capture().ShouldBe(Capture());
        typeof(SecurityAuthorizationCaptureRequest).GetProperties().ShouldAllBe(property => property.SetMethod == null);
    }

    private static SecurityAuthorizationCaptureRequest Capture() => new(Scope(), new SecurityProfileKey("default"), new AgentDefinitionRevision(0), new ConfigurationVersion(9), Identity());
    private static AgentId AgentId() => new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static SessionId SessionId() => new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static OperationId OperationId() => new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static BeforeRunOperationCorrelation Correlation() => new(OperationId(), null);
    private static SecurityAuthorizationScope Scope() => new(AgentId(), SessionId(), Correlation());
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
