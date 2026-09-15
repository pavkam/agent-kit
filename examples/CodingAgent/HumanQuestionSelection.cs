// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Captures one option selected in the terminal plus optional supplementary text.</summary>
/// <param name="OptionId">The exact option identity selected from the presented prompt.</param>
/// <param name="FreeText">Optional supplementary text entered by the human.</param>
internal readonly record struct HumanQuestionSelection(QuestionOptionId OptionId, string? FreeText);
