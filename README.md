# Worker Presets

Save two named staffing layouts for the current kingdom run. Open **Worker Presets** on a building's worker assignment panel to save, rename, or preview a layout. The preview lists individual plots and separates worker and carrier shortfalls. Choose **Confirm apply** to make changes through the game's worker controls.

## Development

The first local test build is version 0.1.0. Build with the .NET SDK, Python 3, the installed game's managed assemblies, and a BepInEx 5 profile. The project defaults to the Steam installation on drive D and a Thunderstore profile named `Worker Presets Dev`. Pass `-p:GameDir="..."` and `-p:ProfileDir="..."` to override those locations.

```powershell
dotnet build WorkerPresets.csproj -c Release
python package.py
python tests/Native/run.py
```

The ZIP is written to `dist/WorkerPresets-0.1.0.zip`. The native probe uses the packaged DLL and the game's Mono runtime in a separate process. It substitutes the label update, sound, and warning display. Unity engine callbacks can print unresolved-call diagnostics in that process.

Install the DLL from the ZIP into the dedicated profile at `BepInEx/plugins/GKJ-WorkerPresets/WorkerPresets.dll`. Run the checkpoint and staffing checks against a **copy** of a kingdom checkpoint.

```powershell
dotnet run -c Release --project tests/StaffingChecks.csproj -- ".analysis/save-copy/1_human_game.data"
```

For live review, use a separate in-game save slot populated from the copy. The mod-manager profile selects plugins and shares the game's save directory. Verify preview, cancel, confirm, and Continue with duplicate buildings and too few workers. Check controller focus and prompts at 1× and 1.5× UI scale.

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution and commit conventions.

## License

Original mod code is licensed under the [MIT License](LICENSE). Super Fantasy Kingdom and its assets belong to their respective owners.
