// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Architecture.Tests;

using System.Collections.Immutable;
using System.Text.RegularExpressions;

/// <summary>
/// Counts superseded-shape evidence in repository text: obsolete attributes, <c>Legacy*</c> identifiers, obsolete-warning
/// suppressions, and compatibility-created or unpinned remarks.
/// </summary>
/// <remarks>
/// The scanner is purely syntactic and stateless, so it is safe to call concurrently. It exists to enforce the AGENTS.md
/// rule that AgentKit keeps no backwards-compatibility surface; the semantic judgement lives in review.
/// </remarks>
internal static partial class ObsoleteSurfaceScanner
{
    /// <summary>Counts every category of evidence in one file's text.</summary>
    /// <param name="content">The complete file text; never null.</param>
    /// <returns>The positive per-category counts; categories with no evidence are absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    internal static ImmutableDictionary<ObsoleteSurfaceCategory, int> Scan(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var counts = ImmutableDictionary.CreateBuilder<ObsoleteSurfaceCategory, int>();
        Add(counts, ObsoleteSurfaceCategory.ObsoleteAttribute, ObsoleteAttribute().Count(content));
        Add(counts, ObsoleteSurfaceCategory.LegacyIdentifier, LegacyIdentifier().Count(content));
        Add(counts, ObsoleteSurfaceCategory.ObsoleteSuppression, ObsoleteSuppression().Count(content));
        Add(counts, ObsoleteSurfaceCategory.CompatibilityText, CompatibilityText().Count(content));
        return counts.ToImmutable();
    }

    private static void Add(ImmutableDictionary<ObsoleteSurfaceCategory, int>.Builder counts, ObsoleteSurfaceCategory category, int count)
    {
        if (count > 0)
        {
            counts[category] = count;
        }
    }

    [GeneratedRegex(@"\[\s*(?:global::)?(?:System\.)?Obsolete\b", RegexOptions.CultureInvariant)]
    private static partial Regex ObsoleteAttribute();

    [GeneratedRegex(@"Legacy[A-Z]", RegexOptions.CultureInvariant)]
    private static partial Regex LegacyIdentifier();

    [GeneratedRegex(@"#pragma\s+warning\s+disable[^\r\n]*\bCS0(?:612|618)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ObsoleteSuppression();

    [GeneratedRegex(@"compatibility-created|unpinned", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CompatibilityText();
}
