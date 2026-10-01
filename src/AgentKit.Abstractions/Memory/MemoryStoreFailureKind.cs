// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies why a memory, document, or vector store refused or could not perform an operation.</summary>
/// <remarks>The vocabulary is shared by the three independent state axes so conformance suites, observation, and policy can reason about refusals uniformly. Kinds never reveal whether an inaccessible item exists.</remarks>
public enum MemoryStoreFailureKind
{
    /// <summary>The grant was missing, expired, already consumed, or did not bind this exact operation.</summary>
    Denied = 0,

    /// <summary>The item does not exist, belongs to another tenant, agent, or principal, or is not visible to the authorized scope.</summary>
    NotFound = 1,

    /// <summary>The expected version or active-version pointer did not match the stored state.</summary>
    VersionConflict = 2,

    /// <summary>An idempotency key or identity was reused with a different request.</summary>
    IdempotencyConflict = 3,

    /// <summary>The requested lifecycle or publication change is not allowed from the stored state.</summary>
    InvalidTransition = 4,

    /// <summary>The request claims an owner, tenant, or agent other than the authorized scope.</summary>
    ScopeMismatch = 5,

    /// <summary>The complete vector-space descriptor of the request does not match the index.</summary>
    IncompatibleVectorSpace = 6,

    /// <summary>A declared size, count, or page bound would be exceeded.</summary>
    LimitExceeded = 7,

    /// <summary>The store cannot currently perform the operation, or does not support it.</summary>
    Unavailable = 8,
}
