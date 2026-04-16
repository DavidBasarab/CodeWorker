## Medium Priority (Workflow Completeness)

6. **Failed vs Blocked distinction** — README defines both states but only Blocked (non-zero exit) is implemented. No separation between infrastructure failures and logical blockers
7. **Task completion validation** — Only checks exit code. No verification that build succeeds, tests pass, or changes were actually produced
8. **Branch switching** — `Branch` config exists but no logic to check out a specific branch before processing
9. **System prompt file** — `SystemPromptFile` config exists but is never read or passed to Claude
10. **Settings merge logic** — Repo settings should selectively override global defaults; unclear if partial overrides work correctly

## Low Priority (Nice-to-Have)

11. **Notifications** — Config model exists (`OnTaskComplete`, `OnTaskFailed`, `OnAllTasksComplete`) but no implementation
12. **Planning phase** — `RunPlanningPhase` config flag exists but no planning mode implementation
13. **Comprehensive structured logging** — README lists ~15 log events with fields like elapsed duration and commit hash; current logging is basic
14. **Windows Task Scheduler setup script** — README has a PowerShell example but no actual automation script in the repo
