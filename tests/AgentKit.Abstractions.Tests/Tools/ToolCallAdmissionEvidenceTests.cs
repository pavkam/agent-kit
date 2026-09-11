// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolCallAdmissionEvidence behavior and contracts.</summary>
public sealed class ToolCallAdmissionEvidenceTests
{
    [Fact]
    public void ToolCallAdmissionEvidence_Constructor_WhenOrdinalNegative_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallAdmissionEvidence(new ToolCatalogVersion("catalog"), -1, new InputFingerprint("sha256:raw")));
        exception.ParamName.ShouldBe("sourceOrdinal");
    }
}
