# Halawa-Hub — Agent Rules

Project: C#/.NET 8 + Avalonia 11.1.3 game-library launcher, Arabic RTL UI.
Repo: github.com/MOOODEGRX-nah/halawa-hub
Current version: v0.0.10.58 (B22 merged — Steam works on Linux)

## Hard rules
- NEVER push to main. Branches only: fix/<task> or feat/<task>.
- NEVER run destructive commands (rm -rf outside /tmp, git reset --hard, git clean -fd, git push --force) without explicit approval in this session.
- NEVER edit .github/workflows/* unless the task explicitly says so.
- Version bumps ONLY via: bash tools/bump.sh X.Y.Z.W
- After a bump, replace the "[PLACEHOLDER...]" line in CHANGELOG.json with a real Arabic note, then run: bash tools/check-meta.sh (must print "meta OK").
- Before every commit: check-meta.sh passes AND `dotnet build HalawaHub.sln -c Release --nologo` AND `dotnet test HalawaHub.sln -c Release --nologo` pass locally.
- Local Linux green is necessary but NOT sufficient: GitHub Actions on windows-latest is the only release oracle. Never claim Windows behavior is verified.
- Commit style: "vX.Y.Z.W fix: ..." / "add: ..." / "chore: ...".
- One logical change per branch.
- User data is sacred: ~/.local/share/HalawaHub (Linux) and %LOCALAPPDATA%\HalawaHub (Windows) must keep backward-compatible reads.
- Keep: net8.0 TFM, Arabic UI + RTL default, PowerShell fallback in Xbox provider.
- Add regression tests with every fix (tests/HalawaHub.Tests, xUnit + FluentAssertions + Moq; InternalsVisibleTo configured on Core).
- Report every change as file:line in your final summary.
- Stop and ask when a task touches Windows-only behavior (Registry/PowerShell/Xbox) you cannot test locally on Linux.
- History note: git-push.sh was deleted in T3 (it bypassed CI). If it reappears on disk, report it — do not resurrect it.
