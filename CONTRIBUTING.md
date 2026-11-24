# Contributing to Worker Presets

## Report a bug

Open a [GitHub issue](https://github.com/GKJ-Super-Fantasy-Kingdom/worker-presets/issues) with the mod, game, and BepInEx versions, your kingdom and day, and the steps to reproduce the problem. Include the relevant log excerpt. For interface or input issues, include the UI scale and whether you used a controller or keyboard and mouse.

## Submit a change

Fork the repository, create a branch, and describe how to build and test your change. Use the assemblies from your own game installation and BepInEx profile as build references. Keep game binaries, save files, and local build output out of commits.

Describe the player-visible change in your pull request. List the checks you ran and, where relevant, which controller actions and UI scales you tested.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) for each commit. Start the subject with a type, an optional scope, and a short description. Use an exclamation mark before the colon for a breaking change.

Examples include `feat(ui): show controller prompts`, `fix(save): restore the current day`, and `chore: update build tooling`.

Accepted types are `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`. Use the same format for pull request titles so GitHub can use the title as the merge or squash commit subject.


## Development

Build with the .NET SDK, Python 3, the installed game's managed assemblies, and a BepInEx 5 profile. The project defaults to the Steam installation on drive D and a Thunderstore profile named `Worker Presets Dev`. Pass `-p:GameDir="..."` and `-p:ProfileDir="..."` to override those locations.

```powershell
dotnet build WorkerPresets.csproj -c Release
python package.py
python tests/Native/run.py
```

The ZIP is written to `dist/WorkerPresets-0.1.3.zip`. The native probe uses the packaged DLL and the game's Mono runtime in a separate process. It substitutes the label update, sound, and warning display. Unity engine callbacks can print unresolved-call diagnostics in that process.

Install the DLL from the ZIP into the dedicated profile at `BepInEx/plugins/GKJ-WorkerPresets/WorkerPresets.dll`. Run the checkpoint and staffing checks against a **copy** of a kingdom checkpoint.

```powershell
dotnet run -c Release --project tests/StaffingChecks.csproj -- ".analysis/save-copy/1_human_game.data"
```

For live review, use a separate in-game save slot populated from the copy. The mod-manager profile selects plugins and shares the game's save directory. Verify preview, cancel, confirm, and Continue with duplicate buildings and too few workers. Check controller focus and prompts at 1× and 1.5× UI scale.
