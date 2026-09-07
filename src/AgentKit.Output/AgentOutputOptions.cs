// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>Mutable, validated binding options for the built-in output processor.</summary>
public sealed class AgentOutputOptions
{
    /// <summary>Gets or sets the maximum size, in UTF-8 bytes, of an extracted candidate. Defaults to 1 MiB.</summary>
    public int MaximumCandidateBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum canonical UTF-8 size of one schema. Defaults to 256 KiB.</summary>
    /// <value>A positive byte count captured independently for each output profile.</value>
    public int MaximumSchemaBytes { get; set; } = 262_144;

    /// <summary>Gets or sets the maximum nested schema depth, from 1 through 128. Defaults to 64.</summary>
    /// <value>A depth from 1 through 128.</value>
    public int MaximumSchemaDepth { get; set; } = 64;

    /// <summary>Gets or sets the maximum number of values traversed while preflighting one schema. Defaults to 4096.</summary>
    /// <value>A positive node count including assertion and annotation values.</value>
    public int MaximumSchemaNodes { get; set; } = 4_096;

    /// <summary>Gets or sets the maximum nested candidate depth, from 1 through 128, evaluated locally. Defaults to 64.</summary>
    /// <value>A depth from 1 through 128.</value>
    public int MaximumCandidateDepth { get; set; } = 64;

    /// <summary>Gets or sets the maximum number of values traversed while evaluating one candidate. Defaults to 65536.</summary>
    /// <value>A positive node count applied even when a structured mode permits no schema.</value>
    public int MaximumCandidateNodes { get; set; } = 65_536;

    /// <summary>Gets or sets the maximum number of issues retained by a single validation failure. Defaults to 64.</summary>
    public int MaximumValidationIssues { get; set; } = 64;

    /// <summary>
    /// Gets or sets the processor-wide ceiling on validation-retry attempts. The
    /// effective allowed attempts for one definition is the lesser of this
    /// value and that definition's own <see cref="OutputRetryPolicy.MaximumAttempts"/>.
    /// Defaults to 2.
    /// </summary>
    public int MaximumRepairAttempts { get; set; } = 2;

    /// <summary>
    /// Gets or sets whether <see cref="OutputMode.NativeSchema"/> and
    /// <see cref="OutputMode.Prompted"/> definitions without a declared
    /// schema are rejected before extraction. Defaults to
    /// <see langword="true"/>.
    /// </summary>
    public bool RequireSchemaForStructuredModes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a provider is allowed to downgrade a requested
    /// output mode. This option is declared for parity with the full
    /// structured-output architecture but is not yet enforced: mode
    /// negotiation depends on the not-yet-implemented provider capability
    /// profile wiring at the call site that selects a mode before send.
    /// </summary>
    public bool AllowProviderModeDowngrade { get; set; }
}
