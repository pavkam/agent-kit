// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Discriminates which member of the closed <see cref="OperationCorrelation"/> hierarchy a persisted correlation document reconstructs.</summary>
/// <remarks>
/// <para>
/// <see cref="OperationCorrelation"/> is polymorphic, and the three concrete kinds carry different identities: a before-run
/// correlation has an optional admission receipt and no run, an in-run correlation has an active run and optional turn, and an
/// after-run correlation has a settled causal run. JSON has no portable representation of a closed class hierarchy, so
/// <see cref="JsonOperationCorrelation"/> flattens all three shapes into one document and uses this enum as the explicit
/// discriminator that selects the correct domain constructor on read.
/// </para>
/// <para>
/// Members start at one so that the default numeric value, which is what a truncated or field-missing document decodes to,
/// is never a valid kind. Reconstruction therefore fails closed instead of silently inventing a before-run correlation for a
/// document whose discriminator was lost. Both the member names and their numeric values are part of the persisted contract;
/// renaming or renumbering a member is a storage-format break.
/// </para>
/// </remarks>
public enum JsonOperationCorrelationKind
{
    /// <summary>The document reconstructs a <see cref="BeforeRunOperationCorrelation"/>, which carries an optional admission receipt and no run identity.</summary>
    BeforeRun = 1,

    /// <summary>The document reconstructs an <see cref="InRunOperationCorrelation"/>, which requires an active run identity and carries an optional turn identity.</summary>
    InRun = 2,

    /// <summary>The document reconstructs an <see cref="AfterRunOperationCorrelation"/>, which requires the already-settled causal run identity.</summary>
    AfterRun = 3,
}
