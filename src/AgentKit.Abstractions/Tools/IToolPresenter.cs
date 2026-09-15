// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects an exact captured formatter or produces bounded generic presentation without tool effects.</summary>
public interface IToolPresenter
{
    /// <summary>Presents projected tool evidence under explicit bounds.</summary>
    /// <param name="request">The nonnull source, optional captured descriptor, and limits.</param>
    /// <param name="cancellationToken">Cancels formatting and propagates cancellation.</param>
    /// <returns>A bounded immutable presentation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    public ValueTask<ToolPresentation> PresentAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default);
}
