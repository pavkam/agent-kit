# AgentKit.Context.Project

Workspace project instruction discovery for AgentKit context assembly.

Register with `AddProjectInstructionContributor` after `AddAgentContext` and a
keyed file-system profile that supports reads. Name the profile with
`ProjectInstructionOptions.ProfileKey` and the absolute directory behind it with
`ProjectInstructionOptions.HostRootPath`, which has no default. Every
instruction file is read through that profile's `IFileReader` after the captured
security authority allows the exact read. See
[ServiceExtensions.cs](ServiceExtensions.cs).
