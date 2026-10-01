// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Names the bounded operations an evaluation result store performs.</summary>
internal enum EvaluationResultStoreOperationKind
{
    /// <summary>Appending one result.</summary>
    Append = 0,

    /// <summary>Reading one page of results.</summary>
    Read = 1,
}
