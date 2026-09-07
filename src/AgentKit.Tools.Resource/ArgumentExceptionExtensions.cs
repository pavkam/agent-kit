// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Resource;

using System.Runtime.CompilerServices;

/// <summary>Provides resource-catalog guards not available in the base class library.</summary>
public static class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <summary>Throws when two definitions use the same stable resource identity.</summary>
        /// <param name="resources">The immutable non-null resource definitions.</param>
        /// <param name="paramName">The caller expression inferred when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="resources"/> contains a duplicate identity.</exception>
        public static void ThrowIfDuplicateResourceIds(
            ImmutableArray<FileResourceDefinition> resources,
            [CallerArgumentExpression(nameof(resources))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefault(resources, paramName);
            if (resources.Select(static resource => resource.Id).Distinct().Count() != resources.Length)
            {
                throw new ArgumentException("Resource IDs must be unique.", paramName);
            }
        }
    }
}
