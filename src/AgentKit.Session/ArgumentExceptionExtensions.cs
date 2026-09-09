// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

/// <summary>Provides canonical package-local argument guards for explicit session-store and entry-codec composition.</summary>
/// <remarks>The guards keep descriptor capture and duplicate-identity validation consistent across immutable catalogs and selectors. They are called through <see cref="ArgumentException"/> so composition failures retain an exact caller argument name.</remarks>
internal static class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <summary>Throws when a composed session-entry codec exposes no immutable descriptor.</summary>
        /// <param name="descriptor">The descriptor captured once from a non-null codec.</param>
        /// <param name="paramName">The codec collection parameter attributed to invalid composition.</param>
        /// <exception cref="ArgumentException"><paramref name="descriptor"/> is null.</exception>
        internal static void ThrowIfNullSessionEntryCodecDescriptor(
            [NotNull] SessionEntryCodecDescriptor? descriptor,
            [CallerArgumentExpression(nameof(descriptor))] string? paramName = null)
        {
            if (descriptor is null)
            {
                throw new ArgumentException("Every composed session-entry codec must expose an immutable descriptor.", paramName);
            }
        }

        /// <summary>Throws when codec bindings reuse a local type or stable wire identity.</summary>
        /// <param name="bindings">The captured bindings to inspect.</param>
        /// <param name="paramName">The parameter attributed to duplicate composition.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bindings"/> is null.</exception>
        /// <exception cref="ArgumentException">Two bindings reuse a writer type or wire identity.</exception>
        internal static void ThrowIfDuplicateSessionEntryCodecBindings(
            IReadOnlyList<SessionEntryCodecBinding> bindings,
            [CallerArgumentExpression(nameof(bindings))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(bindings, paramName);
            var types = new HashSet<Type>();
            var identifiers = new HashSet<SessionEntryTypeId>();
            foreach (var binding in bindings)
            {
                if (!types.Add(binding.Descriptor.EntryType) || !identifiers.Add(binding.Descriptor.TypeId))
                {
                    throw new ArgumentException("Session-entry codec local types and wire identities must be unique.", paramName);
                }
            }
        }

        /// <summary>Throws when a composed session store exposes no immutable descriptor.</summary>
        /// <param name="descriptor">The descriptor captured exactly once from one non-null store.</param>
        /// <param name="paramName">The composed store collection parameter attributed to the invalid entry.</param>
        /// <exception cref="ArgumentException"><paramref name="descriptor"/> is null.</exception>
        internal static void ThrowIfNullSessionStoreDescriptor(
            [NotNull] SessionStoreDescriptor? descriptor,
            [CallerArgumentExpression(nameof(descriptor))] string? paramName = null)
        {
            if (descriptor is null)
            {
                throw new ArgumentException("Every composed session store must expose an immutable descriptor.", paramName);
            }
        }

        /// <summary>Throws when two captured session-store bindings reuse the same explicit store key.</summary>
        /// <param name="bindings">The non-null, completely captured bindings to inspect after descriptor validation.</param>
        /// <param name="paramName">The composed store collection parameter attributed to duplicate registration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bindings"/> is null.</exception>
        /// <exception cref="ArgumentException">Two captured bindings have the same ordinal store key.</exception>
        internal static void ThrowIfDuplicateSessionStoreBindingKeys(
            IReadOnlyList<SessionStoreBinding> bindings,
            [CallerArgumentExpression(nameof(bindings))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(bindings, paramName);
            var keys = new HashSet<SessionStoreKey>();
            foreach (var binding in bindings)
            {
                if (!keys.Add(binding.Descriptor.Key))
                {
                    throw new ArgumentException("Composed session-store keys must be unique.", paramName);
                }
            }
        }
    }
}
