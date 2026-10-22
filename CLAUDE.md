# Working conventions for Claude Code in this repository

## Branches

- Name branches by what they change, in lowercase kebab-case with a type prefix:
  `fix/lesson-times`, `feature/data-export`, `refactor/payments-service`, `docs/readme-setup`.
- Do not use `claude/...` names or random suffixes. If the session assigns one and the owner has
  named a branch in the task, use the owner's name.

## Commits

- Author commits as the repository owner, not as Claude:
  `git -c user.name="yt314" -c user.email="y556780305@gmail.com" commit ...`
- Credit Claude with exactly one trailer at the end of the message, and nothing else:

  ```
  Co-Authored-By: Claude <noreply@anthropic.com>
  ```

- Do not add a `Claude-Session:` line, session links, or a model name to commit messages.
- Write the subject in the style already used here (`fix(scope): ...`, `refactor(scope): ...`,
  `ci: ...`), imperative mood, describing the change rather than how it was produced.

## Pull requests

- Describe the change, why, and how it was tested.
- End the description with a single line: `Co-authored with Claude Code.` No session links.

## Code, comments and docs

- Do not mention Claude, AI or the session in code comments, docs or file names, except where
  the product's own AI lesson planner is the subject.
