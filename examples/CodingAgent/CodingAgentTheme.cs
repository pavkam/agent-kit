// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Resolves which bundled SharpVision theme the application starts with and lists the
/// themes the View menu can switch to at run time.</summary>
/// <remarks>
/// The default is the bundled <c>turbo-vision</c> theme. <c>CODING_AGENT_THEME</c> selects any other
/// catalog slug; an unknown slug falls back to the default rather than failing startup, because a
/// theme is presentation, not configuration the run depends on.
/// </remarks>
internal static class CodingAgentTheme
{
    /// <summary>The catalog slug used when no valid override is configured.</summary>
    public const string DefaultSlug = "turbo-vision";

    /// <summary>The environment variable that overrides the startup theme.</summary>
    public const string EnvironmentVariable = "CODING_AGENT_THEME";

    /// <summary>Gets every bundled theme slug in catalog order.</summary>
    public static IReadOnlyList<string> Slugs => ThemeCatalog.Slugs;

    /// <summary>Resolves the startup theme slug from an optional override.</summary>
    /// <param name="configured">The raw override, typically the environment variable; null or blank selects the default.</param>
    /// <returns>A slug guaranteed to exist in <see cref="Slugs"/>.</returns>
    public static string ResolveSlug(string? configured)
    {
        var slug = configured?.Trim().ToLowerInvariant();
        return slug is { Length: > 0 } && Slugs.Contains(slug, StringComparer.Ordinal) ? slug : DefaultSlug;
    }

    /// <summary>Resolves the startup theme slug from the process environment.</summary>
    /// <returns>A slug guaranteed to exist in <see cref="Slugs"/>.</returns>
    public static string ResolveSlugFromEnvironment() =>
        ResolveSlug(Environment.GetEnvironmentVariable(EnvironmentVariable));

    /// <summary>Loads a frozen theme for one catalog slug.</summary>
    /// <param name="slug">A slug from <see cref="Slugs"/>.</param>
    /// <returns>The frozen bundled theme.</returns>
    /// <exception cref="ArgumentException"><paramref name="slug"/> is blank.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="slug"/> is not a bundled theme.</exception>
    public static Theme Load(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return ThemeCatalog.Load(slug);
    }

    /// <summary>Builds a readable menu label for one slug, such as <c>Tokyo Night Storm</c>.</summary>
    /// <param name="slug">A non-blank catalog slug.</param>
    /// <returns>The slug with hyphens replaced by spaces and each word capitalized.</returns>
    /// <exception cref="ArgumentException"><paramref name="slug"/> is blank.</exception>
    public static string DisplayName(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return string.Join(' ', slug.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(static word => word.Length == 1
                ? word.ToUpperInvariant()
                : char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
