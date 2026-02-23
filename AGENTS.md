# AI Agent Operating Guide

This repository is optimized for AI-agent-assisted development.

## Agent boundaries
- Implement only scoped tasks tied to an issue.
- Do not introduce unrelated refactors.
- Keep changes minimal and reversible.

## Required PR content
- Linked issue
- Acceptance criteria checklist
- Verification steps and outputs
- Risk and rollback notes

## Quality gates
- CI checks must pass.
- Security scanning checks must pass.
- Accuracy-sensitive changes require metrology impact note.

## Coding principles
- Prefer clear, explicit interfaces.
- Preserve units and coordinate-system assumptions.
- Avoid hidden side effects in pipeline steps.

## Escalation
If requirements are ambiguous, stop and request clarification before implementing assumptions that may affect precision targets.
