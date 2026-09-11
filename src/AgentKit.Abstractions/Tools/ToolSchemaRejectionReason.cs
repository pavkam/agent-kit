// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies local schema compilation failure without including schema or provider content.</summary>
public enum ToolSchemaRejectionReason
{
    /// <summary>The explicit schema dialect is not supported by the selected engine.</summary>
    UnsupportedDialect,
    /// <summary>A schema keyword, reference mechanism, or nested dialect declaration is unsupported.</summary>
    UnsupportedKeyword,
    /// <summary>A supported keyword has an invalid shape or the JSON contains duplicate object members.</summary>
    InvalidSchema,
    /// <summary>Schema bytes, nesting, nodes, or deterministic processing work exceed a configured or implementation bound.</summary>
    ResourceLimitExceeded,
}
