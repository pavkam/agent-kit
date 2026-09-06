# Agent-First Repository Design

**Date:** 2026-09-06
**Status:** Approved for specification
**Project:** Agent Framework

## Context

Agent Framework will be a fully featured .NET library for building agentic systems. The repository currently contains only its license and a short README, so it needs a durable development contract before product code is introduced.

The repository will use agent-first development: work is specified before implementation, documentation evolves with the code, reusable repository skills encode recurring workflows, and each completed task ends in a verified commit pushed to the remote repository.

## Goals

- Make the repository's operating rules discoverable to humans and coding agents.
- Require an approved specification before behavior or architecture changes.
- Keep product, architecture, and decision documentation synchronized with implementation.
- Provide reusable skills for spec-driven delivery and documentation synchronization.
- Define an auditable completion path that includes verification, commit, and push.
- Keep this setup independent of future .NET solution, package, target-framework, and public-API decisions.

## Non-Goals

- Create the .NET solution or any source or test project.
- Select target frameworks, dependencies, package boundaries, CI providers, or release tooling.
- Design the public API or runtime architecture.
- Add generated content or dependencies.

## Approaches Considered

### 1. Lean agent-first baseline (selected)

Add the repository contract, living-documentation structure and templates, two focused repository skills, contribution and pull-request conventions, and repository hygiene files. This gives future work a complete delivery loop while preserving product-architecture choices for their own specs.

### 2. Baseline plus .NET skeleton

Also create an empty solution, projects, shared build properties, and CI. This would make the repository immediately buildable, but it would prematurely choose package and project boundaries without an approved architecture specification.

### 3. Documentation-only minimum

Add only `AGENTS.md` and a documentation index. This is smaller, but it leaves recurring workflows implicit and makes documentation drift and incomplete task handoff more likely.

## Design

### Repository contract

A root `AGENTS.md` will be the authoritative instruction entry point. It will state the project mission, instruction precedence, docs-first workflow, documentation ownership, verification expectations, and the requirement that every completed task is committed and pushed.

The contract will distinguish product behavior, architectural decisions, and implementation plans:

- Product or behavior changes require an approved specification.
- Durable architectural decisions require an architecture decision record (ADR).
- Multi-step implementation uses a checked-in implementation plan linked to its specification.
- Code and its affected living documentation ship in the same task.

### Living documentation

`docs/README.md` will provide the documentation map and lifecycle. The initial hierarchy will contain:

- `docs/product/README.md` for the mission, scope, and product principles.
- `docs/architecture/README.md` for the eventual system map and component boundaries.
- `docs/specs/README.md` for active and historical feature specifications.
- `docs/decisions/README.md` for ADRs.
- `docs/plans/README.md` for implementation plans.
- `docs/development/workflow.md` for the end-to-end task workflow.
- `docs/templates/spec.md` and `docs/templates/adr.md` for consistent durable records.

This design record remains under `docs/superpowers/specs/` because it records the approved setup design produced by the repository-design workflow. Future product specs use `docs/specs/`.

Indexes are living documents: a task that creates, supersedes, or implements a document updates the relevant index in the same commit.

### Repository skills

Two focused skills will live under `.agents/skills/`:

1. `spec-driven-development` guides agents from request classification through an approved specification, implementation plan, and traceable execution.
2. `sync-living-docs` identifies documentation affected by a change and verifies the relevant indexes, specs, architecture notes, ADRs, and contributor guidance remain accurate.

Skills will contain valid YAML frontmatter, explicit triggers, bounded responsibilities, and concise step-by-step instructions. They will reference repository docs instead of duplicating detailed policy.

### Contributor workflow and hygiene

`CONTRIBUTING.md` and `.github/pull_request_template.md` will expose the same workflow to human contributors. `.editorconfig`, `.gitattributes`, and a .NET-aware `.gitignore` will provide deterministic text and repository hygiene without introducing tooling dependencies.

The root README will retain the project description and add the documentation and contribution entry points.

### Task lifecycle

Every task follows this sequence:

1. Read `AGENTS.md` and the documentation map.
2. Confirm a current approved specification exists, or create and approve one before implementation.
3. Create an implementation plan when the work is multi-step.
4. Implement in small verified increments.
5. Update affected living documentation and indexes.
6. Run the smallest verification that proves the change, plus repository-wide checks warranted by risk.
7. Review the diff and ensure no secrets, generated output, or unrelated changes are present.
8. Commit with a descriptive message and push the completed task to the remote branch.

If verification fails, documentation is stale, the diff is not understood, or the push does not succeed, the task is not complete.

## Error Handling and Boundaries

- Missing or ambiguous specifications block implementation and are escalated to the task owner.
- Documentation conflicts are resolved by updating the authoritative living document and recording an ADR when the resolution is architectural.
- A failed verification, commit, or push is reported explicitly; completion is never claimed from an unpushed working tree.
- Agents do not expand scope into .NET architecture or product code during this setup.

## Verification

The setup will be verified by:

- checking that every required file exists;
- checking that Markdown links and referenced repository paths resolve;
- validating repository skill frontmatter and required sections;
- scanning for placeholders and contradictory workflow language;
- running `git diff --check`;
- reviewing the complete staged diff;
- committing and pushing to `origin/main`; and
- confirming local `HEAD` matches `origin/main` after the push.

## Acceptance Criteria

- A root `AGENTS.md` defines the spec-first, living-docs, verification, commit, and push contract.
- The documented hierarchy and templates exist and are linked from `docs/README.md`.
- The two repository skills are discoverable under `.agents/skills/` and encode their focused workflows.
- Contributor and pull-request guidance reflects the same contract.
- Repository hygiene files are present without adding runtime or build dependencies.
- The README directs contributors and agents to the correct entry points.
- No .NET solution, runtime code, package layout, or public API is introduced.
- Verification passes and the completed task is committed and pushed.
