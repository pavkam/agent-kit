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
    }
}
