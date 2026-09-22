// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Builds run-bound <see cref="IToolCatalogCapture"/> evidence for the legacy tool adapter.</summary>
/// <remarks>
/// The first-party legacy executor uses this factory until run-bound discovery capture replaces the singleton catalog
/// (workstream 4, chunks C7b and C10b).
/// </remarks>
public interface IToolRunCatalogCaptureFactory
{
    /// <summary>Captures immutable catalog evidence for one run.</summary>
    /// <param name="request">The nonnull run binding and authorization evidence.</param>
    /// <returns>An owned capture whose snapshot reflects the registered legacy catalog.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public IToolCatalogCapture Create(RunToolCatalogCaptureRequest request);

    /// <summary>Asynchronously captures immutable catalog evidence for one run.</summary>
    /// <param name="request">The nonnull run binding and authorization evidence.</param>
    /// <param name="cancellationToken">Cancels capture before ownership transfers.</param>
    /// <returns>An owned capture for the requested tool surface.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<IToolCatalogCapture> CreateAsync(
        RunToolCatalogCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Create(request));
    }
}
