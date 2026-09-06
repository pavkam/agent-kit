// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The action an <see cref="ISessionRetentionPolicy"/> decided for one session.</summary>
public enum SessionRetentionAction
{
    /// <summary>Take no action; the session remains active as-is.</summary>
    Keep,

    /// <summary>Move the session to long-term, read-mostly storage.</summary>
    Archive,

    /// <summary>Delete the session and its entire record.</summary>
    Delete
}
