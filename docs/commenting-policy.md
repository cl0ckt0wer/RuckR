# Commenting Policy (C#)

This repo uses three complementary commenting policies.

## 1) Public API XML Docs Are Required

Scope:
- `public` and `protected` types and members in shared/reusable code.

Rules:
- Use XML docs (`///`) with `<summary>`.
- Add `<param>`, `<returns>`, and `<exception>` when applicable.
- Keep documentation aligned with behavior changes in the same PR.

Notes:
- XML documentation generation is enabled in project files.
- Build enforcement can be toggled via `EnforcePublicApiXmlDocs` in `Directory.Build.props`.

## 2) Prefer `<inheritdoc/>` Over Duplicated Docs

Scope:
- Overrides, interface implementations, and members with inherited behavior.

Rules:
- Use `<inheritdoc/>` when inherited semantics are unchanged.
- Add extra remarks only for behavior differences.

Why:
- Reduces stale copy-paste docs and keeps IntelliSense consistent.

## 3) Inline Comments Explain Why, Not What

Scope:
- Method bodies, reducers, controller logic, and integration workflows.

Rules:
- Comment intent, tradeoffs, edge cases, and external constraints.
- Avoid narrating obvious code steps.
- Remove or update comments when code changes invalidate them.

Good examples:
- Workaround rationale for framework/library behavior.
- Business rule context that cannot be inferred from names alone.
- Ordering/consistency constraints across async or distributed flows.

