// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityPolicySnapshotId behavior and contracts.</summary>
public sealed class SecurityPolicySnapshotIdTests: Conformance.GuidIdentityConformanceTests<SecurityPolicySnapshotId>
{
    /// <summary>Verifies snapshot identifiers reject the default GUID with the exact parameter name.</summary>
    [Fact]
    public void Constructor_WhenSnapshotIdIsEmpty_ThrowsExactArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SecurityPolicySnapshotId(Guid.Empty));
        exception.ParamName.ShouldBe("value");
    }

    /// <inheritdoc/>
    protected override SecurityPolicySnapshotId Create(Guid value) => new(value);
    /// <inheritdoc/>
    protected override Guid GetValue(SecurityPolicySnapshotId subject) => subject.Value;
    /// <summary>Verifies valid scalar values preserve their exact identity and diagnostic representation.</summary>
    [Fact]
    public void Constructor_WhenScalarValuesAreValid_PreservesValues()
    {
        var id = new SecurityPolicySnapshotId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        id.ToString().ShouldBe("11111111-1111-1111-1111-111111111111");
        default(SecurityPolicySnapshotId).Value.ShouldBe(Guid.Empty);
    }
}
