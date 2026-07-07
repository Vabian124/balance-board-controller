# Contributing

Thanks for improving Balance Board Controller. This project is optimized for both human and **AI-assisted** development.

## For AI coding agents

**Start with [AGENTS.md](AGENTS.md).** It links to architecture, file map, glossary, and roadmap.

Machine-readable index: [llms.txt](llms.txt)

## For humans

1. Fork / branch from `main`
2. Read [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)
3. Make focused changes; run `dotnet build BalanceBoard.sln -c Release`
4. Open a PR with a short summary and test notes

## Scope guidelines

- Keep UI in `BalanceBoard.App`, logic in `BalanceBoard.Core`
- One concern per PR when possible
- Update `docs/ROADMAP.md` when completing planned items
- Do not include personal data, logs, or local reference folders

## Questions

Open a GitHub issue on [balance-board-controller](https://github.com/Vabian124/balance-board-controller).
