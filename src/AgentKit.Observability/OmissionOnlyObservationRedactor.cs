// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability;

/// <summary>Redacts every observation payload by omitting content, preserving structural export without capture.</summary>
/// <remarks>This is the default redactor registered by <see cref="ServiceExtensions.AddAgentKitObservability"/>.</remarks>
public sealed class OmissionOnlyObservationRedactor: IObservationRedactor
{
    /// <inheritdoc/>
    public ValueTask<RedactionResult> RedactAsync(
        ObservationContent content,
        ObservationPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<RedactionResult>(new ContentOmitted());
    }
}
