// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed outcomes of one <see cref="IToolCallRecorder"/> write.</summary>
/// <remarks>A recorder never signals failure through a nullable success; callers pattern-match the two sealed outcomes.</remarks>
public abstract record ToolCallRecordResult
{
    /// <summary>Initializes one of the two supported recording outcomes.</summary>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed recording family.</exception>
    private protected ToolCallRecordResult() =>
        ArgumentException.ThrowIfNotEqual(this is ToolCallRecorded or ToolCallRecordRejected, true, "result");

    /// <summary>Copies the base state of a supported immutable recording outcome.</summary>
    /// <param name="original">The nonnull original result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is null.</exception>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed recording family.</exception>
    protected ToolCallRecordResult(ToolCallRecordResult original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(this is ToolCallRecorded or ToolCallRecordRejected, true, "result");
    }
}
