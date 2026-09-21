// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records how one contributed source was projected into the assembled model request.</summary>
public enum ContextManifestDisposition
{
    /// <summary>The source content was included without material transformation.</summary>
    Included,

    /// <summary>The source content was included after an adapter-permitted transformation.</summary>
    Transformed,

    /// <summary>The source content was omitted from the assembled request.</summary>
    Omitted,
}
