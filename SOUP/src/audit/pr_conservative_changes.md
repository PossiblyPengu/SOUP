PR: Conservative audit fixes — theme consolidation + candidate removals

Summary
- Make `OrderLogWidgetElevatedCardStyle` BasedOn the global `ElevatedCardStyle` (reduces duplicated corner/shadow/padding rules).
- Do NOT remove code automatically. Instead propose a small, conservative set of high-confidence private-field removals for manual review.

Files changed (this PR)
- `src/Features/OrderLog/Themes/OrderLogWidgetTheme.xaml` — now uses `BasedOn="{StaticResource ElevatedCardStyle}"` and keeps widget-specific overrides.

Proposed safe removal candidates (manual review required)
These are conservative picks from the vetted CSV; please review before merging.

1. `_unusedExampleField` in `SomeFile.cs` — (example placeholder) *REVIEW NEEDED*
2. (Replace this list with actual candidates after manual confirmation)

Why not remove everything now?
- Many private fields are used via XAML bindings, reflection, or source generators; automated deletion risks breaking runtime behavior.

Next steps
1. Manually review `src/vetted_unused_private_fields_full.csv` focusing on fields with `TotalReferences=0` and small declaration scope.
2. Confirm any candidate is not used by XAML bindings, reflection, or tests; then remove in a follow-up tiny PR (1–3 fields per PR).
3. Run full `dotnet build` and smoke-test the UI (especially widgets and settings) after each removal.

If you want, I can now:
- (A) Populate the candidate list with 10 conservative items and create a branch + commit (no deletions), or
- (B) Open a branch and remove 2–3 highest-confidence fields and run a build/test cycle.
