// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Json;

/// <summary>Configures the evidence bounds and encoding contract of a JSON goal store.</summary>
public sealed class JsonGoalStoreOptions
{
    /// <summary>Gets or sets the largest encoded record, in bytes.</summary>
    /// <value>A positive bound. The default is one mebibyte.</value>
    public int MaximumRecordBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the largest encoded manifest document, in bytes.</summary>
    /// <value>A positive bound. The default is one mebibyte.</value>
    public int MaximumDocumentBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the record count above which initialization compacts the log to one snapshot per goal.</summary>
    /// <value>A positive threshold. The default is 4096.</value>
    public int CompactionRecordThreshold { get; set; } = 4_096;

    /// <summary>Gets or sets the scanner identities allowed to call <see cref="IGoalStore.ReadIntentsAsync"/>.</summary>
    /// <value>A list that is empty by default: intent discovery crosses tenants and is refused unless the host names its worker.</value>
    public List<ComponentId> AuthorizedIntentScanners { get; } = [];

    /// <summary>Gets or sets the JSON encoding contract.</summary>
    /// <value>The strict canonical contract by default.</value>
    public JsonEncodingSettings Encoding { get; set; } = JsonEncodingSettings.CreateDefault();
}
