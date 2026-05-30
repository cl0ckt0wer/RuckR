# Webwright for RuckR

Webwright is installed as a sibling checkout at:

```powershell
C:\Users\clock\source\repos\Webwright
```

The checkout has an isolated Python environment at `.venv`, the `webwright` package is installed editable, and Playwright Chromium/Firefox browser runtimes are installed for that environment.

## Run the Production Smoke Task

From the RuckR repo:

```powershell
pwsh ./scripts/run-webwright-ruckr-prod-smoke.ps1
```

Generated scripts, logs, and screenshots are written under:

```powershell
.artifacts/webwright/
```

That folder is already ignored by git.

The Webwright CLI needs one model API key in the environment:

```powershell
$env:OPENAI_API_KEY = "<key>"
```

Use `-ModelConfig model_claude.yaml` with `ANTHROPIC_API_KEY`, or `-ModelConfig model_openrouter.yaml` with `OPENROUTER_API_KEY`.

For authenticated production smoke coverage, provide credentials through environment variables only:

```powershell
$env:RUCKR_PROD_SMOKE_EMAIL = "<email>"
$env:RUCKR_PROD_SMOKE_PASSWORD = "<password>"
```

Do not put production credentials in the task text, scripts, docs, or committed files.

## Codex Plugin Notes

The Webwright repository includes Codex plugin metadata and skills under `skills/webwright/`. The marketplace was registered locally with:

```powershell
codex plugin marketplace add C:\Users\clock\source\repos\Webwright
```

If a new Codex session exposes the `webwright` skill, use it for exploratory browser tasks that should produce a rerunnable `final_script.py` plus screenshot evidence. If the plugin is unavailable, use the script above; it drives the upstream Webwright CLI directly.
