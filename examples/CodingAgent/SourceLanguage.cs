// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Maps file paths and provider-neutral language hints to exact <c>CodeView</c> catalog names.</summary>
/// <remarks>The bundled syntax catalog in this SharpVision build only ships a curated ~160-language subset (no
/// plain JSON, YAML, or Markdown grammar, for one) rather than every mainstream language; unmapped or
/// unsupported extensions fall back to <see langword="null"/> (plain, unhighlighted, but still line-preserving
/// <c>CodeView</c> text) rather than guessing at a name the catalog would reject.</remarks>
internal static class SourceLanguage
{
    /// <summary>Resolves the exact catalog language name for a workspace-relative file path, or null when no
    /// bundled grammar covers its extension.</summary>
    public static string? ForPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        // Verified one-by-one against this build's actual bundled catalog (Resources/syntax.manifest.json) — it
        // covers only ~160 mostly niche grammars and is missing several mainstream languages entirely (Python,
        // Ruby, Go, SQL, YAML, and plain JSON among them; JSON5 is a strict enough superset of JSON to stand in
        // for it). CodeView.Language throws KeyNotFoundException for a name the catalog doesn't have, so a wrong
        // guess here would crash the transcript instead of just rendering unhighlighted; every case below is a
        // name confirmed present, and anything else deliberately falls through to plain text.
        var extension = Path.GetExtension(path);
        return extension.ToLowerInvariant() switch
        {
            ".cs" or ".csx" => "C#",
            ".json5" => "JSON5",
            ".jsonnet" or ".libsonnet" => "Jsonnet",
            ".hjson" => "Hjson",
            ".ts" or ".mts" or ".cts" => "TypeScript",
            ".tsx" => "TypeScript React (TSX)",
            ".jsx" => "JavaScript React (JSX)",
            ".rs" => "Rust",
            ".swift" => "Swift",
            ".ps1" or ".psm1" or ".psd1" => "PowerShell",
            ".c" or ".h" => "ANSI C89",
            ".csv" => "CSV",
            ".tsv" or ".tab" => "TSV",
            ".sh" or ".bash" or ".zsh" => "Zsh",
            _ => null,
        };
    }

    /// <summary>The catalog language name for tool call/result JSON payloads.</summary>
    public const string Json = "JSON5";

    /// <summary>Maps a provider-neutral presentation language hint to a verified bundled catalog name.</summary>
    /// <param name="language">The optional lower-case language hint supplied by a tool formatter.</param>
    /// <returns>A supported catalog name, or <see langword="null"/> for plain fixed-width rendering.</returns>
    public static string? ForPresentation(string? language) => language?.ToLowerInvariant() switch
    {
        "csharp" or "cs" => "C#",
        "json" or "json5" => Json,
        "typescript" => "TypeScript",
        "rust" => "Rust",
        "shell" or "sh" or "bash" or "zsh" => "Zsh",
        "powershell" => "PowerShell",
        _ => null,
    };

}
