# Suggestions backlog

Improvement ideas identified by architecture review — **not yet implemented**.

## Purpose

This folder is a living backlog of code quality, architecture, testing, and documentation improvements. Items here are proposals until someone implements them and marks status.

## Workflow

1. **Pick an item** from `2026-07-07-code-review.md` (or add a new dated report).
2. **Implement** in a focused PR; reference the item ID if useful.
3. **Mark done** in the report (`Proposed` → `Done`).
4. **Promote** stable conventions into permanent docs using `promotion-checklist.md`.
5. **Archive** promoted items as `Promoted` so the backlog stays honest.

## Status legend

| Status | Meaning |
|--------|---------|
| **Proposed** | Identified, not started |
| **In Progress** | Someone is actively working on it (note assignee/PR in the item) |
| **Done** | Implemented and merged |
| **Promoted** | Convention copied into AGENTS.md, llms.txt, .cursor/rules, or CONTRIBUTING.md |

## Files

| File | Contents |
|------|----------|
| [2026-07-07-code-review.md](2026-07-07-code-review.md) | Main review report |
| [coding-standards-draft.md](coding-standards-draft.md) | Draft rules for CONTRIBUTING / STANDARDS.md |
| [promotion-checklist.md](promotion-checklist.md) | What to copy where after implementation |

## In-flight work (do not duplicate)

Other agents may be implementing: weight display, tare/calibrate UI, pairing speed, vJoy spam fixes. Cross-reference those PRs when closing related items here.
