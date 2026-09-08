// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

using System.Runtime.CompilerServices;

/// <summary>Provides package-specific argument guards for security-authority composition.</summary>
internal static class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <summary>Throws when a materialized authority-binding snapshot contains a duplicate key.</summary>
        /// <param name="bindings">The complete materialized binding snapshot to inspect before constructing a lookup map.</param>
        /// <param name="paramName">The composition argument responsible for an invalid or duplicate binding.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bindings"/> or one of its entries is null.</exception>
        /// <exception cref="ArgumentException">Two entries carry the same authority component key.</exception>
        public static void ThrowIfDuplicateSecurityAuthorityBinding(
            IReadOnlyList<SecurityAuthorityBinding> bindings,
            [CallerArgumentExpression(nameof(bindings))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(bindings, paramName);
            var keys = new HashSet<ComponentKey<ISecurityAuthority>>();
            foreach (var binding in bindings)
            {
                ArgumentNullException.ThrowIfNull(binding, paramName);
                if (!keys.Add(binding.Key))
                {
                    throw new ArgumentException($"Security authority key '{binding.Key}' is registered more than once.", paramName);
                }
            }
        }
    }
}
