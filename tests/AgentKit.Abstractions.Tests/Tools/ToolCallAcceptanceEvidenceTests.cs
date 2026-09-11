// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolCallAcceptanceEvidence behavior and contracts.</summary>
public sealed class ToolCallAcceptanceEvidenceTests
{
    [Fact]
    public void ToolCallAcceptanceEvidence_Constructor_WhenFingerprintDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallAcceptanceEvidence(new GrantId(Guid.NewGuid()), default, DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("validatedArgumentsFingerprint");
    }
}
