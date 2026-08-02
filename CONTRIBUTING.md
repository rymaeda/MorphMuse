# Contributing Guidelines

## Project Hygiene

- Do not keep dead/unused code in the repository "just in case". If a file is not referenced
  by any `<Compile Include>` in the `.csproj` (e.g. old/legacy implementations), remove it.
  Git history already preserves prior versions; there is no need for a `legacy/` folder.
- Prefer deleting superseded implementations once a fix has been validated, instead of leaving
  multiple dated copies (e.g. `SurfaceBuilderCopilot20250922.cs`, `SurfaceBuilderCopilot20250928a.cs`).
- Keep debug/log messages (`CamBam.ThisApplication.AddLogMessage`) limited to actionable
  warnings/errors. Avoid leaving verbose step-by-step "success" logging added for one-off
  troubleshooting sessions; clean it up once the issue is resolved.