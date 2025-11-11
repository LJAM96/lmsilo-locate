# Repository Guidelines

## Project Structure & Module Organization
- Root files: `GeoLens.sln`, `GeoLens.csproj`, `App.xaml(.cs)` and the WinUI views in `Views/`.
- `Assets/` and `Properties/` hold static media and manifest tweaks; keep UI resources isolated there.
- `Core/` houses the inference helpers (`predictor/` dataclasses, `api_service.py`) and the CLI smoke test.
- `Docs/` documents architecture, wireframes, and future work in the numbered pattern already in place.

## Build, Test, and Development Commands
- `dotnet restore GeoLens.sln` (NuGet dependencies).
- `dotnet build GeoLens.sln` (compile the shell and flag XAML issues).
- `dotnet run --project GeoLens.csproj` (launch the desktop shell once the API is running).
- `python -m pip install -r Core/requirements.txt` (or the GPU-specific variant you need).
- `uvicorn Core.api_service:app --reload` (exercise the FastAPI endpoint during UI work).
- `python -m Core.smoke_test --device auto` (confirm GeoCLIP still predicts after dependency changes).

## Coding Style & Naming Conventions
- Use four-space indentation everywhere; prefer PascalCase for public C# types, camelCase for private fields, and matching class names between layers.
- Keep XAML attributes tidy (one object per line for readability) and avoid code-behind logic unless view models cannot yet represent the state.
- Python helpers should stay typed (`Path`, dataclasses) and rely on `Core/predictor` structures so the CLI and API stay in sync.
- Run `dotnet format` when touching many C# files, and keep new Python code compatible with standard PEP 8 spacing.

## Testing Guidelines
- Extend `Core/smoke_test.py` when adding inference behavior and rerun it after installing new Torch builds.
- Document any manual verification steps in `Docs/`; once automated suites exist, mirror the current naming style (e.g., `test_predictor_csv_loading`).

## Commit & Pull Request Guidelines
- Favor `type(scope): short description` (e.g., `feat(core): add caching toggle`) and describe how you verified the change.
- PRs must summarize what changed, list the commands used to test locally, and include screenshots whenever UI surfaces shift.

## Security & Configuration Tips
- Never check Hugging Face tokens or cache paths into the repo; rely on `set_hf_cache_environment` or environment variables instead.
- Keep the `requirements-*.txt` files updated whenever you touch native dependencies so contributors know which stack to install.
