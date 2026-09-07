// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

using System.Runtime.CompilerServices;

/// <summary>Provides the canonical duplicate-skill-identity guard.</summary>
public static class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <summary>Throws when a captured skill sequence repeats a public identity.</summary>
        /// <param name="skills">The initialized sequence to inspect.</param>
        /// <param name="paramName">The caller expression used as the exception parameter name.</param>
        /// <exception cref="ArgumentException">At least two definitions have the same identity.</exception>
        public static void ThrowIfDuplicateSkillIds(
            ImmutableArray<SkillDefinition> skills,
            [CallerArgumentExpression(nameof(skills))] string? paramName = null)
        {
            if (skills.Select(static skill => skill.Id).Distinct().Count() != skills.Length)
            {
                throw new ArgumentException("Skill identities must be unique.", paramName);
            }
        }
    }
}
