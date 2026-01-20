Conservative Candidates — Top 20 (manual review required)

These fields were flagged by an automated scan as having zero textual references. Do NOT remove automatically — manually verify XAML bindings, reflection, and source-generator uses before deletion.

How to review each candidate:
- Open the declaring file and search for the field name in XAML, strings, reflection calls, or expressions passed to `nameof()`.
- Grep the repo for the exact field name (case-sensitive) to confirm no hidden references.
- Run the application or relevant feature manual smoke tests after removal.

Candidates:

1. `_adornerLayer` — src/Behaviors/ListBoxDragDropBehavior.cs:32
2. `_albumArt` — src/Services/SpotifyService.cs:39
3. `_allItems` — src/ViewModels/DictionaryManagementViewModel.cs:33
4. `_allocationBuddyHandler` — src/ViewModels/UnifiedSettingsViewModel.cs:36
5. `_allStores` — src/ViewModels/DictionaryManagementViewModel.cs:34
6. `_analytics` — src/ViewModels/ExpireWiseViewModel.cs:206
7. `_appBarCallbackId` — src/Windows/OrderLogWidgetWindow.xaml.cs:32
8. `_applicationHandler` — src/ViewModels/UnifiedSettingsViewModel.cs:35
9. `_appName` — src/ViewModels/AllocationBuddySettingsViewModel.cs:19
10. `_artistName` — src/Services/SpotifyService.cs:36
11. `_artRetryCts` — src/Services/SpotifyService.cs:44
12. `_autoLoadLastSession` — src/ViewModels/AllocationBuddySettingsViewModel.cs:40
13. `_autoRefreshIntervalMinutes` — src/ViewModels/EssentialsBuddySettingsViewModel.cs:31
14. `_autoSaveIntervalMinutes` — src/ViewModels/AllocationBuddySettingsViewModel.cs:31
15. `_availableMonths` — src/ViewModels/ExpireWiseViewModel.cs:71
16. `_availableStores` — src/ViewModels/ExpirationItemDialogViewModel.cs:36
17. `_availableUpdate` — src/ViewModels/MainWindowViewModel.cs:47; src/Windows/OrderLogWidgetWindow.xaml.cs:40
18. `_averageShelfLife` — src/ViewModels/ExpireWiseAnalyticsViewModel.cs:43
19. `_bcService` — src/ViewModels/ExternalDataViewModel.cs:17
20. `_bcTestResult` — src/ViewModels/ExternalDataViewModel.cs:37

Suggested next actions:
- Mark each as "reviewed" after manual checks.
- For safe removals, create a PR removing 1–3 fields at a time with a build and smoke-test.
