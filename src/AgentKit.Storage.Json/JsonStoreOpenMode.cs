// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Defines whether trusted bootstrap may create the configured JSON store root and its manifest.</summary>
public enum JsonStoreOpenMode
{
    /// <summary>Requires an already initialized store root containing a valid manifest.</summary>
    OpenExisting,

    /// <summary>Permits creating the configured root directory, its manifest, and its empty record logs.</summary>
    CreateIfMissing,
}
