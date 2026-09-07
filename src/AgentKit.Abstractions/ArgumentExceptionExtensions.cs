// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Runtime.CompilerServices;

/// <summary>
/// Reusable <see cref="ArgumentException"/> guard clauses for constraints the
/// base class library does not already expose as a <c>ThrowIf*</c> member.
/// </summary>
/// <remarks>
/// AgentKit's argument-validation convention is to use the BCL's own
/// <c>ArgumentException.ThrowIf*</c>/<c>ArgumentNullException.ThrowIf*</c>/
/// <c>ArgumentOutOfRangeException.ThrowIf*</c> guard-clause methods at every
/// public and internal boundary, called through the exception type itself
/// (for example, <c>ArgumentException.ThrowIfNullOrWhiteSpace(value)</c>).
/// When a genuinely reusable constraint has no matching BCL member — such as
/// rejecting a default, uninitialized <see cref="ImmutableArray{T}"/> — this
/// class adds exactly one canonical extension for it, following the same
/// calling convention, rather than letting every call site repeat the
/// condition inline or invent a competing helper.
/// </remarks>
public static class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if <paramref name="array"/>
        /// is a default, uninitialized <see cref="ImmutableArray{T}"/>.
        /// </summary>
        /// <remarks>
        /// A default <see cref="ImmutableArray{T}"/> has no backing storage
        /// at all: <see cref="ImmutableArray{T}.IsDefault"/> is
        /// <see langword="true"/>, and enumerating or indexing it throws a
        /// confusing <see cref="NullReferenceException"/> far away from
        /// where the bad value actually originated. AgentKit's collection
        /// parameters must always be either a genuinely populated array or
        /// the explicit <see cref="ImmutableArray{T}.Empty"/> sentinel, so
        /// this guard turns that mistake into an immediate, clearly
        /// attributed argument error at the constructor boundary instead.
        /// </remarks>
        /// <typeparam name="T">The array element type.</typeparam>
        /// <param name="array">The candidate array.</param>
        /// <param name="paramName">
        /// The name of the validated parameter. Inferred automatically from
        /// the call-site expression via
        /// <see cref="CallerArgumentExpressionAttribute"/> when not
        /// supplied explicitly.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="array"/> is a default, uninitialized array.
        /// </exception>
        public static void ThrowIfDefault<T>(
            ImmutableArray<T> array,
            [CallerArgumentExpression(nameof(array))] string? paramName = null)
        {
            if (array.IsDefault)
            {
                throw new ArgumentException(
                    "Value must not be a default ImmutableArray<T>. Use ImmutableArray<T>.Empty.",
                    paramName);
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if <paramref name="array"/>
        /// is default or contains no elements.
        /// </summary>
        /// <typeparam name="T">The array element type.</typeparam>
        /// <param name="array">The candidate array that must contain at least one element.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="array"/> is default or empty.</exception>
        public static void ThrowIfDefaultOrEmpty<T>(
            ImmutableArray<T> array,
            [CallerArgumentExpression(nameof(array))] string? paramName = null)
        {
            if (array.IsDefaultOrEmpty)
            {
                throw new ArgumentException("Value must contain at least one element.", paramName);
            }
        }

        /// <summary>Throws an <see cref="ArgumentException"/> if an immutable reference array is default or contains null.</summary>
        /// <typeparam name="T">The non-null reference element type.</typeparam>
        /// <param name="array">The candidate array.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="array"/> is default or contains a null element.</exception>
        public static void ThrowIfContainsNull<T>(
            ImmutableArray<T> array,
            [CallerArgumentExpression(nameof(array))] string? paramName = null)
            where T : class
        {
            ArgumentException.ThrowIfDefault(array, paramName);
            if (array.Any(static value => value is null))
            {
                throw new ArgumentException("Value must not contain null elements.", paramName);
            }
        }

        /// <summary>Throws an <see cref="ArgumentException"/> if a string contains the NUL character.</summary>
        /// <param name="value">The non-null candidate string.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="value"/> contains a NUL character.</exception>
        public static void ThrowIfContainsNul(
            string value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(value, paramName);
            if (value.Contains('\0', StringComparison.Ordinal))
            {
                throw new ArgumentException("Value must not contain NUL characters.", paramName);
            }
        }

        /// <summary>Throws when a host is not a canonicalizable DNS name or unscoped IP literal.</summary>
        /// <param name="value">The candidate host text.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is blank, contains a scoped-address marker, or is not a DNS/IP host.
        /// </exception>
        public static void ThrowIfInvalidNetworkHost(
            string value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
            var candidate = value.Trim().TrimEnd('.');
            if (candidate.Contains('%', StringComparison.Ordinal)
                || Uri.CheckHostName(candidate) == UriHostNameType.Unknown)
            {
                throw new ArgumentException("Value must be a DNS name or an unscoped IP-address literal.", paramName);
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if <paramref name="uri"/>
        /// is not an absolute URI.
        /// </summary>
        /// <param name="uri">The candidate URI.</param>
        /// <param name="paramName">
        /// The name of the validated parameter, inferred from the call-site
        /// expression when omitted.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="uri"/> is not absolute.</exception>
        public static void ThrowIfNotAbsoluteUri(
            Uri uri,
            [CallerArgumentExpression(nameof(uri))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(uri, paramName);

            if (!uri.IsAbsoluteUri)
            {
                throw new ArgumentException("Value must be an absolute URI.", paramName);
            }
        }

        /// <summary>Throws when a web-search result URL is not credential-free absolute HTTP(S).</summary>
        /// <param name="uri">The absolute URI to validate.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentException">The scheme is not HTTP(S), the host is missing, or user information is present.</exception>
        public static void ThrowIfInvalidWebResultUri(
            Uri uri,
            [CallerArgumentExpression(nameof(uri))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(uri, paramName);
            if (!uri.IsAbsoluteUri
                || uri.Scheme is not ("http" or "https")
                || string.IsNullOrWhiteSpace(uri.Host)
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new ArgumentException(
                    "Value must be an absolute credential-free HTTP(S) URI with a host.",
                    paramName);
            }
        }

        /// <summary>Throws when a configured web-search destination is not a network endpoint resource.</summary>
        /// <param name="resource">The resource to validate.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="resource"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="resource"/> is not a network endpoint.</exception>
        public static void ThrowIfNotNetworkEndpointResource(
            ProtectedResource resource,
            [CallerArgumentExpression(nameof(resource))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(resource, paramName);
            if (resource.Kind != ProtectedResourceKind.NetworkEndpoint)
            {
                throw new ArgumentException("Value must identify a network endpoint.", paramName);
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if <paramref name="path"/>
        /// is not a non-rooted relative URI path.
        /// </summary>
        /// <remarks>
        /// Rooted, authority-relative, and absolute values are rejected so
        /// resolving the path against a trusted base address cannot replace
        /// that address's origin or base path.
        /// </remarks>
        /// <param name="path">The candidate relative URI path.</param>
        /// <param name="paramName">
        /// The name of the validated parameter, inferred from the call-site
        /// expression when omitted.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="path"/> is empty, whitespace, rooted, authority-relative,
        /// or an absolute URI.
        /// </exception>
        public static void ThrowIfNotRelativeUriPath(
            string path,
            [CallerArgumentExpression(nameof(path))] string? paramName = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path, paramName);

            if (!Uri.TryCreate(path, UriKind.Relative, out _) || path[0] is '/' or '\\')
            {
                throw new ArgumentException("Value must be a non-rooted relative URI path.", paramName);
            }
        }

        /// <summary>Throws when human-question options contain duplicate identities.</summary>
        /// <param name="options">The initialized option array to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="options"/> is default or contains duplicate identities.</exception>
        public static void ThrowIfDuplicateQuestionOptionIds(
            ImmutableArray<HumanQuestionOption> options,
            [CallerArgumentExpression(nameof(options))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefault(options, paramName);
            if (options.Select(static option => option.Id).Distinct().Count() != options.Length)
            {
                throw new ArgumentException("Question option identities must be unique.", paramName);
            }
        }

        /// <summary>Throws when work-plan items contain duplicate stable identities.</summary>
        /// <param name="items">The initialized item array to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="items"/> is default or contains duplicate identities.</exception>
        public static void ThrowIfDuplicatePlanItemIds(
            ImmutableArray<WorkPlanItem> items,
            [CallerArgumentExpression(nameof(items))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefault(items, paramName);
            if (items.Select(static item => item.Id).Distinct().Count() != items.Length)
            {
                throw new ArgumentException("Plan item identities must be unique.", paramName);
            }
        }

        /// <summary>Throws when more than one work-plan item is in progress.</summary>
        /// <param name="items">The initialized item array to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="items"/> is default or contains multiple in-progress items.</exception>
        public static void ThrowIfMultipleInProgressPlanItems(
            ImmutableArray<WorkPlanItem> items,
            [CallerArgumentExpression(nameof(items))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefault(items, paramName);
            if (items.Count(static item => item.Status == PlanItemStatus.InProgress) > 1)
            {
                throw new ArgumentException("At most one plan item may be in progress.", paramName);
            }
        }

        /// <summary>Throws when a stream cannot supply artifact content.</summary>
        /// <param name="stream">The non-null stream to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="stream"/> is not readable.</exception>
        public static void ThrowIfNotReadable(
            Stream stream,
            [CallerArgumentExpression(nameof(stream))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(stream, paramName);
            if (!stream.CanRead)
            {
                throw new ArgumentException("Stream must be readable.", paramName);
            }
        }
    }
}
