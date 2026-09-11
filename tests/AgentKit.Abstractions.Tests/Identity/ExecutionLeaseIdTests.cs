// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ExecutionLeaseId behavior and contracts.</summary>
public sealed class ExecutionLeaseIdTests: Conformance.GuidIdentityConformanceTests<ExecutionLeaseId>
{
    /// <inheritdoc/>
    protected override ExecutionLeaseId Create(Guid value) => new(value);
    /// <inheritdoc/>
    protected override Guid GetValue(ExecutionLeaseId subject) => subject.Value;
    [Fact]
    public void GuidIdentity_Constructor_WhenValueIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionLeaseId(Guid.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void GuidIdentity_Constructor_WhenValueIsPopulated_Succeeds()
    {
        var identity = new ExecutionLeaseId(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        identity.ToString().ShouldBe("11111111-2222-3333-4444-555555555555");
    }
}
