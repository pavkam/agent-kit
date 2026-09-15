// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>
/// Loads the prompt text this package ships as embedded resources, so the
/// default <see cref="CompactionOptions.SummaryPrompt"/> is authored in a
/// reviewable text file rather than a C# string literal.
/// </summary>
/// <remarks>
/// <para>
/// Each resource is read from the assembly manifest exactly once, on first
/// access, and cached for the lifetime of the process. Loading is
/// thread-safe: concurrent first readers share one load, and a failed load
/// is cached and rethrown to every later reader so a missing resource is
/// deterministic rather than intermittent.
/// </para>
/// <para>
/// The resource is decoded as UTF-8 with any byte-order mark removed and
/// trailing whitespace trimmed; leading text is preserved verbatim so the
/// authored prompt is what the model receives.
/// </para>
/// </remarks>
internal static class CompactionPromptResources
{
    /// <summary>
    /// The stable manifest logical name of the default summary prompt resource, pinned by the project file so a
    /// rename of the source file or root namespace cannot silently change the lookup key.
    /// </summary>
    internal const string DefaultSummaryPromptResourceName =
        "AgentKit.Context.Compaction.Resources.DefaultCompactionSummaryPrompt.txt";

    /// <summary>
    /// Upper bound on the resource size this loader will read, so a corrupted or accidentally bloated resource
    /// fails at load time rather than being sent to a provider on every compaction.
    /// </summary>
    private const int _maximumResourceBytes = 64 * 1024;

    private static readonly Lazy<string> _defaultSummaryPrompt = new(
        static () => LoadText(DefaultSummaryPromptResourceName));

    /// <summary>
    /// Gets the default system instruction a model-backed compaction strategy sends when the application does
    /// not configure <see cref="CompactionOptions.SummaryPrompt"/>.
    /// </summary>
    /// <value>Non-empty prompt text with no leading byte-order mark and no trailing whitespace.</value>
    /// <exception cref="InvalidOperationException">
    /// The embedded resource is missing from the assembly, exceeds the size bound, or contains no non-whitespace
    /// text. The same exception is rethrown on every access after the first failure.
    /// </exception>
    internal static string DefaultSummaryPrompt => _defaultSummaryPrompt.Value;

    /// <summary>Reads one embedded text resource from this assembly's manifest.</summary>
    private static string LoadText(string resourceName)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(resourceName), "Callers pass a compile-time constant resource name.");

        var assembly = typeof(CompactionPromptResources).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"The embedded resource '{resourceName}' is missing from the {assembly.GetName().Name} assembly; "
                + "the package build is incomplete.");

        if (stream.Length > _maximumResourceBytes)
        {
            throw new InvalidOperationException(
                $"The embedded resource '{resourceName}' is {stream.Length} bytes, exceeding the {_maximumResourceBytes}-byte bound.");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd().TrimEnd();
        return string.IsNullOrWhiteSpace(text)
            ? throw new InvalidOperationException($"The embedded resource '{resourceName}' contains no prompt text.")
            : text;
    }
}
