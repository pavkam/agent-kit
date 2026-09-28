// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns one execution or recovery attempt's activated durability services.</summary>
public interface IDurabilityRuntimeLease: IAsyncDisposable
{
    /// <summary>Gets the captured context fixed for this attempt.</summary>
    public DurableExecutionContext Context { get; }

    /// <summary>Gets the selected backend.</summary>
    public IDurableExecutionBackend Backend { get; }

    /// <summary>Gets the selected journal.</summary>
    public IDurableOperationJournal Journal { get; }

    /// <summary>Gets the selected lease manager.</summary>
    public IDurableLeaseManager LeaseManager { get; }

    /// <summary>Gets the selected recovery policy.</summary>
    public IRecoveryPolicy RecoveryPolicy { get; }
}
