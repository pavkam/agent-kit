// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;



/// <summary>Verifies SecurityGrantStoreUnavailableException behavior and contracts.</summary>
public sealed class SecurityGrantStoreUnavailableExceptionTests
{
    /// <summary>Verifies typed unavailable failures reject undefined kinds and unsafe blank explanations.</summary>
    [Fact]
    public void Constructor_WhenUnavailableEvidenceIsInvalid_ThrowsExactArgument()
    {
        var kind = Should.Throw<ArgumentOutOfRangeException>(() => new SecurityGrantStoreUnavailableException((SecurityGrantStoreFailureKind) 99, "safe"));
        var message = Should.Throw<ArgumentException>(() => new SecurityGrantStoreUnavailableException(SecurityGrantStoreFailureKind.OpenFailed, " "));
        kind.ParamName.ShouldBe("kind");
        message.ParamName.ShouldBe("safeMessage");
    }
}
