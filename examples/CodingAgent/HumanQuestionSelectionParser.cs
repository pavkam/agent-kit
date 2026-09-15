// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using System.Globalization;

/// <summary>Parses the terminal's numbered choice syntax against the exact presented options.</summary>
internal static class HumanQuestionSelectionParser
{
    /// <summary>Parses one option number followed by optional supplementary text.</summary>
    /// <param name="text">The terminal composer text.</param>
    /// <param name="options">The exact options displayed to the user.</param>
    /// <param name="allowsFreeText">Whether text after the option number is valid.</param>
    /// <param name="selection">The parsed selection when successful.</param>
    /// <returns><see langword="true"/> only when the number and optional text satisfy the displayed prompt.</returns>
    internal static bool TryParse(
        string text,
        ImmutableArray<HumanQuestionOption> options,
        bool allowsFreeText,
        out HumanQuestionSelection selection)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfContainsNull(options);
        var trimmed = text.Trim();
        var separator = trimmed.IndexOfAny([' ', '\t', '\r', '\n']);
        var choice = separator < 0 ? trimmed : trimmed[..separator];
        var freeText = separator < 0 ? null : trimmed[(separator + 1)..].Trim();
        if (!int.TryParse(choice, CultureInfo.InvariantCulture, out var optionNumber)
            || optionNumber < 1
            || optionNumber > options.Length
            || (!allowsFreeText && !string.IsNullOrWhiteSpace(freeText)))
        {
            selection = default;
            return false;
        }

        selection = new HumanQuestionSelection(
            options[optionNumber - 1].Id,
            string.IsNullOrWhiteSpace(freeText) ? null : freeText);
        return true;
    }
}
