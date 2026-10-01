// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json;

/// <summary>Configures the bounds and encoding contract of a JSON evaluation result store.</summary>
public sealed class JsonEvaluationStoreOptions
{
    /// <summary>Gets or sets the largest encoded result record, in bytes.</summary>
    /// <value>A positive bound. The default is one mebibyte. A larger result is rejected as limit exceeded.</value>
    public int MaximumRecordBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the largest encoded manifest document, in bytes.</summary>
    /// <value>A positive bound. The default is one mebibyte.</value>
    public int MaximumDocumentBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the JSON encoding contract.</summary>
    /// <value>The strict canonical contract by default.</value>
    public JsonEncodingSettings Encoding { get; set; } = JsonEncodingSettings.CreateDefault();
}
