// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionCutSelectionFailed behavior and contracts.</summary>
public sealed class CompactionCutSelectionFailedTests
{
    [Fact]
    public void CompactionCutSelectionFailed_Constructor_WhenFailureNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CompactionCutSelectionFailed(null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void CompactionCutSelectionFailed_Equality_WhenSameValues_InstancesAreEqual() => new CompactionCutSelectionFailed(Failure()).ShouldBe(new CompactionCutSelectionFailed(Failure()));
    private static CompactionFailure Failure() => new(CompactionFailureKind.Unknown, "unknown", retryable: false, ExtensionData.Empty);
}
