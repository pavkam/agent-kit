// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionValidationFailed behavior and contracts.</summary>
public sealed class CompactionValidationFailedTests
{
    [Fact]
    public void CompactionValidationFailed_Constructor_WhenFailureNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CompactionValidationFailed(null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void CompactionValidationFailed_Equality_WhenSameValues_InstancesAreEqual() => new CompactionValidationFailed(Failure()).ShouldBe(new CompactionValidationFailed(Failure()));
    private static CompactionFailure Failure() => new(CompactionFailureKind.Unknown, "unknown", retryable: false, ExtensionData.Empty);
}
