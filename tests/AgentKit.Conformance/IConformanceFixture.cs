// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>
/// Creates one conformance subject through the implementation's ordinary
/// composition path.
/// </summary>
/// <typeparam name="TContract">The public contract under test.</typeparam>
/// <remarks>
/// Fixtures own lifetime. <see cref="CreateAsync"/> must use the same
/// registration an application would, not a test-only constructor that skips
/// validation. <see cref="Capabilities"/> is read before any case that might
/// be optional; it cannot turn a required case off.
/// </remarks>
public interface IConformanceFixture<TContract>: IAsyncDisposable
    where TContract : class
{
    /// <summary>Gets the optional behaviors this subject declared before use.</summary>
    /// <value>The capability flags. The default requires every optional behavior.</value>
    public ConformanceCapabilities Capabilities { get; }

    /// <summary>Creates a subject through its public composition path.</summary>
    /// <param name="cancellationToken">Cancels creation before the subject is returned.</param>
    /// <returns>The subject the suite will exercise. The fixture retains disposal ownership.</returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was already canceled, or creation was canceled before a subject existed.
    /// </exception>
    public ValueTask<TContract> CreateAsync(CancellationToken cancellationToken = default);
}
