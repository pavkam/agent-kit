// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Test-only helpers that vary one component key of an <see cref="AgentDefinition"/>.</summary>
public static class AgentDefinitionTestExtensions
{
    extension(AgentDefinition definition)
    {
        /// <summary>Returns a copy whose component selection replaces only the supplied keys.</summary>
        /// <param name="loop">The replacement loop key, or null to keep the current one.</param>
        /// <param name="continuationPolicy">The replacement continuation-policy key, or null to keep the current one.</param>
        /// <param name="input">The replacement input-coordinator key, or null to keep the current one.</param>
        /// <param name="output">The replacement output-publisher key, or null to keep the current one.</param>
        /// <param name="outputProcessor">The replacement output-processor key, or null to keep the current one.</param>
        /// <param name="context">The replacement context-assembler key, or null to keep the current one.</param>
        /// <param name="modelSelector">The replacement model-selector key, or null to keep the current one.</param>
        /// <param name="modelExecutor">The replacement model-executor key, or null to keep the current one.</param>
        /// <param name="budgetProfile">The replacement budget-profile key, or null to keep the current one.</param>
        /// <returns>A definition equal to the original except for the replaced component keys.</returns>
        public AgentDefinition WithComponents(
            ComponentKey<IAgentLoop>? loop = null,
            ComponentKey<IRunContinuationPolicy>? continuationPolicy = null,
            ComponentKey<IInputCoordinator>? input = null,
            ComponentKey<IOutputPublisher>? output = null,
            ComponentKey<IOutputProcessor>? outputProcessor = null,
            ComponentKey<IContextAssembler>? context = null,
            ComponentKey<IModelSelector>? modelSelector = null,
            ComponentKey<IModelRequestExecutor>? modelExecutor = null,
            BudgetProfileKey? budgetProfile = null)
        {
            var current = definition.Components;
            return definition with
            {
                Components = new AgentComponentSelection(
                    loop ?? current.Loop,
                    continuationPolicy ?? current.ContinuationPolicy,
                    input ?? current.Input,
                    output ?? current.Output,
                    outputProcessor ?? current.OutputProcessor,
                    context ?? current.Context,
                    modelSelector ?? current.ModelSelector,
                    modelExecutor ?? current.ModelExecutor,
                    budgetProfile ?? current.BudgetProfile),
            };
        }
    }
}
