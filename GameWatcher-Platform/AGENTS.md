# Agents Guidance — G- Editing & Structure
  - Keep changes minimal, focused, and in-place; don't rename projects unless explicitly requested.
  - Prefer dependency injection for new services; avoid hard-wiring dependencies.
  - In docs, prefer ASCII where Unicode bullets/arrows cause patching issues.
  - **Before removing or commenting out service registrations**, check `docs/design/APPLICATION_ARCHITECTURE.md` to verify it's not a mandatory service.
  - **When in doubt about app responsibilities**, reference APPLICATION_ARCHITECTURE.md rather than inferring from current code state.

## V1 (SimpleLoop) as Reference — Porting in V2cher-Platform

Scope: This file applies to the `GameWatcher-Platform/` subtree only. Follow these conventions when editing code and docs here. User/developer prompts still take precedence.

## Project Map

- `GameWatcher.Engine` — OCR, detection, packs, core services
- `GameWatcher.Runtime` — headless orchestrator (hosted services)
- `GameWatcher.Studio` — player GUI (end users)
- `GameWatcher.AuthorStudio` — creator tools (pack authors)
- `FF1.PixelRemaster` — sample/reference pack
- `docs/` — design and user docs (this repo)

**CRITICAL:** Before modifying any application's DI configuration, consult `docs/design/APPLICATION_ARCHITECTURE.md` to verify required services. Each app has MANDATORY service requirements.

## Conventions

- Naming
  - “Studio” = player, “Author Studio” = creator. Keep references consistent in code and docs.
  - Use `GameWatcher.AuthorStudio.exe` in authoring CLI examples.

- Editing & Structure
  - Keep changes minimal, focused, and in-place; don’t rename projects unless explicitly requested.
  - Prefer dependency injection for new services; avoid hard-wiring dependencies.
  - In docs, prefer ASCII where Unicode bullets/arrows cause patching issues.

## V1 (SimpleLoop) as Reference — Porting in V2

V2 code should stand on its own. If a performance‑critical piece is missing, use `Archive/SimpleLoop/` as the authoritative reference but only for the smallest extract that solves the gap.

- Likely relevant sources (examples):
  - `Archive/SimpleLoop/SimpleLoop/CaptureService.cs` — capture loop patterns
  - `Archive/SimpleLoop/SimpleLoop/DynamicTextboxDetector.cs` — detection approach
  - `Archive/SimpleLoop/SimpleLoop/EnhancedOCR.cs` — OCR preprocessing ideas
  - `Archive/SimpleLoop/SimpleLoop/DialogueCatalog.cs` — dialogue catalog shape

- Porting guidance:
  - Re‑implement against V2 abstractions (Engine interfaces) instead of copying V1 types.
  - Remove V1 logging, GUI hooks, and path logic; use V2 logging/options.
  - Keep optimizations; modernize naming, async flows, and nullability.

- Quick check after porting:
  - Build the impacted project(s) and validate basic runtime behavior (no V1‑style console prints, correct DI wiring).

- Build & Run
  - Build all: `dotnet build GameWatcher-Platform.sln -c Release`
  - Run runtime host: `dotnet run --project GameWatcher.Runtime`
  - Run player GUI: `dotnet run --project GameWatcher.Studio`
  - Run authoring tools: `dotnet run --project GameWatcher.AuthorStudio`

- Validation
  - If tests exist, run only those relevant to your changes; otherwise compile the changed projects to validate.
  - Don’t introduce formatters or new CI steps without request.

## Git Management (when user requests)

- Branch naming
  - `feat/runtime-tts-queue`, `fix/ocr-thresholds`, `docs/author-studio-guide`

- Commit messages (Conventional Commits)
  - `feat(runtime): add audio preload cache`
  - `fix(engine): guard null region in detector`
  - `docs: add Author Studio quickstart`

- Suggested flow
  1. `git checkout -b docs/author-studio-quickstart`
  2. Apply changes via `apply_patch`
  3. `git add -A && git commit -m "docs: add Author Studio quickstart"`
  4. `git push -u origin docs/author-studio-quickstart`
  5. Open PR with a concise summary and list of touched files

Note: In this environment, do not run git commands unless the user explicitly asks. Propose the commands instead.
