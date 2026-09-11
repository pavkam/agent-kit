// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityAuditRecordId behavior and contracts.</summary>
public sealed class SecurityAuditRecordIdTests: Conformance.GuidIdentityConformanceTests<SecurityAuditRecordId>
{
    [Fact]
    public void Constructor_WhenAuditRecordIdIsEmpty_ThrowsArgumentOutOfRangeExceptionForValue()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuditRecordId(Guid.Empty));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("value");
    }

    /// <inheritdoc/>
    protected override SecurityAuditRecordId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(SecurityAuditRecordId subject) => subject.Value;
}
