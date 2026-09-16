// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Verifies AgentRunOptions behavior and contracts.</summary>
public sealed class AgentRunOptionsTests
{
    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AgentRunOptions(
            CompositionTestData.SessionId, CompositionTestData.BranchId, null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void WithExpression_WhenIdentityIsReplacedWithAnotherValidIdentity_RetainsTheNewValue()
    {
        var options = CompositionTestData.RunOptions();
        var replacement = TestSupport.TestExecutionIdentity.Create(
            new TenantId("other-tenant"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human);

        var updated = options with { Identity = replacement };

        updated.Identity.ShouldBeSameAs(replacement);
    }

    [Fact]
    public void WithExpression_WhenIdentityIsSetToNull_ThrowsExactArgumentNullException()
    {
        var options = CompositionTestData.RunOptions();

        var exception = Should.Throw<ArgumentNullException>(() => options with { Identity = null! });

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("Identity");
    }
}
