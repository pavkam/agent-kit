// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

/// <summary>Classifies the terminal outcome of one directory observation before it is surfaced as entries or an exception.</summary>
internal enum DirectoryReadStatus
{
    /// <summary>The directory was observed completely.</summary>
    Success,

    /// <summary>The directory does not exist.</summary>
    NotFound,

    /// <summary>The grant was not fresh and exact, or the target crosses a symbolic link or inaccessible boundary.</summary>
    Denied,

    /// <summary>The listing exceeds the configured snapshot bound.</summary>
    LimitExceeded,

    /// <summary>The directory exists but could not be observed.</summary>
    Failed,
}
