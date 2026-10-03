# Change impact and test selection

The TypeScript workspace exposes broad dependencies through packages and organizes consumer tests by area and smoke/regression tags. A backend monolith may place many coupled modules in one .csproj, so project references alone cannot identify all affected behavior. The change-impact tool uses an explicit component map that the backend team owns.

Each component in the JSON manifest has a unique name, repository-relative path globs, names of components it depends on, and a VSTest filter for tests that cover it. If Payments depends on Accounts, changing Accounts affects both. The analyzer walks this relationship transitively. See impact-map.example.json for the framework's own map; its filters deliberately run all framework tests until finer mappings exist.

From the repository root, after fetching enough Git history to include the base revision:

    dotnet run --project TestingCore.Impact.Cli --configuration Release -- . origin/main docs/impact-map.example.json

The CLI compares the merge base of the given revision with HEAD and prints JSON containing changed files, unmapped files, affected components, selection, and recommendedFilter. Selection is targeted when all changes map to components, full when any file is unmapped, and none when there are no changes. A targeted filter can be passed to dotnet test --filter for fast pull request feedback. Run the full suite before release sign-off.

When this framework moves under the backend monolith, replace the example map with paths such as src/Payments/**, src/Accounts/**, and database or event contract paths. Add dependency edges for direct service calls, shared database tables, events, shared DTOs, feature configuration, and integration contracts. Tag the corresponding xUnit tests with [Trait("Component", "Payments")] or use a verified FullyQualifiedName filter. Review the map whenever module ownership changes.

The report is a selection aid, not a coverage proof. Dynamic dispatch, reflection, shared state, infrastructure configuration, and undeclared runtime coupling may escape the map. Unknown files deliberately produce selection=full. Keep the full suite in the release gate and attach the report to the Azure Boards release readiness work item.