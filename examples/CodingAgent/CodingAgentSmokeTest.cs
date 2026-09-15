// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>A headless entry point that exercises <see cref="AgentRuntime"/> without the SharpVision UI.</summary>
internal static class CodingAgentSmokeTest
{
    public static async Task<int> RunAsync(string[] args)
    {
        var workspaceRoot = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        var prompt = args.Length > 1 ? args[1] : "Say hello in one short sentence.";
        var configuration = CodingAgentConfiguration.CreateDefault();

        Console.WriteLine($"Workspace: {workspaceRoot}");
        Console.WriteLine($"Model: {configuration.ModelId}");
        Console.WriteLine($"Prompt: {prompt}");
        Console.WriteLine("---");

        using var conversation = AgentRuntime.Create(
            workspaceRoot,
            OpenAiEnvironment.RequireApiKey(),
            configuration,
            new AutoApprovePrompt(),
            new UnavailableHumanQuestionPrompt(),
            new PermissionModeController());
        var result = await conversation.SendAsync(prompt, CancellationToken.None);

        Console.WriteLine($"Succeeded: {result.Succeeded}");
        foreach (var conversationEvent in result.Events)
        {
            Console.WriteLine(conversationEvent switch
            {
                ConversationAssistantTextEvent text => $"[text] {text.Text}",
                ConversationToolCallEvent call => $"[call] {call.ToolName}({call.ArgumentsJson})",
                ConversationToolResultEvent toolResult => $"[result] {toolResult.ToolName} succeeded={toolResult.Succeeded}: {toolResult.Summary}",
                ConversationUsageEvent usage =>
                    $"[usage] input={usage.Usage.InputTokens} output={usage.Usage.OutputTokens} cost={usage.Usage.EstimatedCost?.ToString() ?? "unknown"}",
                _ => conversationEvent.ToString(),
            });
        }

        return result.Succeeded ? 0 : 1;
    }
}
