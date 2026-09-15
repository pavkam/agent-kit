// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Diagnostics;

/// <summary>
/// Shared composition-time validation for the endpoint members every
/// OpenAI-compatible provider options class exposes: an absolute base
/// address plus non-rooted relative operation paths.
/// </summary>
/// <remarks>
/// <para>
/// Each branded package's <c>IValidateOptions&lt;TOptions&gt;</c>
/// implementation delegates its endpoint checks here so that all leaves
/// enforce one rule set during startup validation. The checks are
/// performed by invoking the very same guards
/// <see cref="OpenAICompatibilityProfile"/>'s constructor applies
/// (<c>ArgumentException.ThrowIfNotAbsoluteUri</c> and
/// <c>ArgumentException.ThrowIfNotRelativeUriPath</c>), so a value that
/// passes options validation can never be rejected later when the profile
/// is built inside a model factory, and a value the profile would reject
/// always fails composition first.
/// </para>
/// <para>
/// Every method returns failure messages rather than throwing, and returns
/// all failures it finds instead of stopping at the first, so a host can
/// report a misconfigured endpoint and a misconfigured path together. The
/// helper is stateless and thread-safe.
/// </para>
/// </remarks>
public static class OpenAICompatibleEndpointOptionsValidation
{
    /// <summary>
    /// Validates the endpoint members of an options class whose endpoint
    /// exposes only a chat completions operation.
    /// </summary>
    /// <param name="baseAddress">The configured base address; must be an absolute URI.</param>
    /// <param name="chatCompletionsPath">The configured chat completions path; must be a non-rooted relative URI path.</param>
    /// <param name="baseAddressName">The options member name reported in a failure about <paramref name="baseAddress"/>.</param>
    /// <param name="chatCompletionsPathName">The options member name reported in a failure about <paramref name="chatCompletionsPath"/>.</param>
    /// <returns>
    /// An empty array when both members are valid; otherwise one
    /// human-readable failure message per invalid member, in argument
    /// order. The messages name the offending member and never echo its
    /// value, so they are safe to surface through
    /// <c>OptionsValidationException</c>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="baseAddressName"/> or <paramref name="chatCompletionsPathName"/> is null, empty, or whitespace.</exception>
    public static ImmutableArray<string> Validate(
        Uri? baseAddress,
        string? chatCompletionsPath,
        string baseAddressName,
        string chatCompletionsPathName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddressName);
        ArgumentException.ThrowIfNullOrWhiteSpace(chatCompletionsPathName);

        var failures = ImmutableArray.CreateBuilder<string>(2);
        AppendBaseAddressFailure(failures, baseAddress, baseAddressName);
        AppendRelativePathFailure(failures, chatCompletionsPath, chatCompletionsPathName);
        return failures.ToImmutable();
    }

    /// <summary>
    /// Validates the endpoint members of an options class whose endpoint
    /// exposes both a chat completions and an embeddings operation.
    /// </summary>
    /// <param name="baseAddress">The configured base address; must be an absolute URI.</param>
    /// <param name="chatCompletionsPath">The configured chat completions path; must be a non-rooted relative URI path.</param>
    /// <param name="embeddingsPath">
    /// The configured embeddings path; must be a non-rooted relative URI
    /// path. A <see langword="null"/> value is a failure here because the
    /// options class declares the member; use the overload without an
    /// embeddings path for endpoints that expose no embeddings operation.
    /// </param>
    /// <param name="baseAddressName">The options member name reported in a failure about <paramref name="baseAddress"/>.</param>
    /// <param name="chatCompletionsPathName">The options member name reported in a failure about <paramref name="chatCompletionsPath"/>.</param>
    /// <param name="embeddingsPathName">The options member name reported in a failure about <paramref name="embeddingsPath"/>.</param>
    /// <returns>
    /// An empty array when all members are valid; otherwise one
    /// human-readable failure message per invalid member, in argument
    /// order. The messages name the offending member and never echo its
    /// value, so they are safe to surface through
    /// <c>OptionsValidationException</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="baseAddressName"/>, <paramref name="chatCompletionsPathName"/>,
    /// or <paramref name="embeddingsPathName"/> is null, empty, or whitespace.
    /// </exception>
    public static ImmutableArray<string> Validate(
        Uri? baseAddress,
        string? chatCompletionsPath,
        string? embeddingsPath,
        string baseAddressName,
        string chatCompletionsPathName,
        string embeddingsPathName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddressName);
        ArgumentException.ThrowIfNullOrWhiteSpace(chatCompletionsPathName);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingsPathName);

        var failures = ImmutableArray.CreateBuilder<string>(3);
        AppendBaseAddressFailure(failures, baseAddress, baseAddressName);
        AppendRelativePathFailure(failures, chatCompletionsPath, chatCompletionsPathName);
        AppendRelativePathFailure(failures, embeddingsPath, embeddingsPathName);
        return failures.ToImmutable();
    }

    /// <summary>Appends a failure when <paramref name="baseAddress"/> would be rejected by <c>ArgumentException.ThrowIfNotAbsoluteUri</c>.</summary>
    /// <param name="failures">The collector receiving the failure message.</param>
    /// <param name="baseAddress">The candidate base address.</param>
    /// <param name="memberName">The options member name reported in the failure.</param>
    private static void AppendBaseAddressFailure(ImmutableArray<string>.Builder failures, Uri? baseAddress, string memberName)
    {
        Debug.Assert(failures is not null, "The failure collector must be supplied by the caller.");
        Debug.Assert(!string.IsNullOrWhiteSpace(memberName), "The member name must already have been validated.");

        if (baseAddress is null || !PassesGuard(() => ArgumentException.ThrowIfNotAbsoluteUri(baseAddress)))
        {
            failures.Add($"{memberName} must be an absolute URI.");
        }
    }

    /// <summary>Appends a failure when <paramref name="path"/> would be rejected by <c>ArgumentException.ThrowIfNotRelativeUriPath</c>.</summary>
    /// <param name="failures">The collector receiving the failure message.</param>
    /// <param name="path">The candidate operation path.</param>
    /// <param name="memberName">The options member name reported in the failure.</param>
    private static void AppendRelativePathFailure(ImmutableArray<string>.Builder failures, string? path, string memberName)
    {
        Debug.Assert(failures is not null, "The failure collector must be supplied by the caller.");
        Debug.Assert(!string.IsNullOrWhiteSpace(memberName), "The member name must already have been validated.");

        if (path is null || !PassesGuard(() => ArgumentException.ThrowIfNotRelativeUriPath(path)))
        {
            failures.Add($"{memberName} must be a non-rooted relative URI path.");
        }
    }

    /// <summary>
    /// Runs one argument guard and reports whether it accepted its input,
    /// translating the guard's <see cref="ArgumentException"/> into a
    /// boolean so options validation shares the guard's exact predicate
    /// without duplicating its condition.
    /// </summary>
    /// <param name="guard">The guard invocation; it either returns or throws an <see cref="ArgumentException"/>.</param>
    /// <returns><see langword="true"/> when the guard returned; <see langword="false"/> when it threw.</returns>
    private static bool PassesGuard(Action guard)
    {
        Debug.Assert(guard is not null, "A guard invocation must be supplied.");

        try
        {
            guard();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
