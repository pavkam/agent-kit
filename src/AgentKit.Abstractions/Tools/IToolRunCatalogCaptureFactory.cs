// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Builds run-bound <see cref="IToolCatalogCapture"/> evidence for one run's tool surface.</summary>
/// <remarks>
/// The first-party implementation captures through the configured <see cref="IToolCatalog"/>; the run-plan compiler
/// resolves it to obtain the capture it hands to <see cref="IToolExecutor"/>.
/// </remarks>
public interface IToolRunCatalogCaptureFactory
{
    /// <summary>Captures immutable catalog evidence for one run.</summary>
    /// <param name="request">The nonnull run binding and authorization evidence.</param>
    /// <returns>An owned capture whose snapshot reflects the configured catalog.</returns>
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
