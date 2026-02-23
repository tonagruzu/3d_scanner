# Contributing

## Workflow
- Use short-lived branches from `main`.
- Open a pull request for every change; direct pushes to `main` are discouraged.
- Link PRs to issues and include test evidence.

## Definition of Done
- Clear acceptance criteria in issue/PR.
- Relevant automated checks pass.
- Documentation updated when behavior or interfaces change.
- No regression in dimensional validation gates.

## Commit and PR conventions
- Prefer focused commits.
- PR title format: `<scope>: <short description>`.
- Include: problem, solution summary, verification steps, and risk notes.

## Testing expectations
- Run build and tests before PR.
- For pipeline-sensitive changes, include a summary of metrology impact.

## Security
- Never commit credentials or sensitive data.
- Use `SECURITY.md` process for vulnerability reporting.
