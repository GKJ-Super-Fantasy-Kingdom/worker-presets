"""Exercise Worker Presets against the installed Mono player assemblies without starting the game."""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
from tempfile import TemporaryDirectory
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]


def run_mono(game, core, exe, arguments):
    runtime = game / "MonoBleedingEdge"
    managed = game / "Super Fantasy Kingdom_Data/Managed"
    paths = os.pathsep.join(map(str, (managed, core, exe.parent)))
    os.environ["MONO_PATH"] = paths
    ctypes.windll.kernel32.SetErrorMode(3)
    with os.add_dll_directory(str(runtime / "EmbedRuntime")):
        mono = ctypes.CDLL(str(runtime / "EmbedRuntime/mono-2.0-bdwgc.dll"))

    def api(name, result, *types):
        function = getattr(mono, name)
        function.restype, function.argtypes = result, types
        return function

    ptr, string = ctypes.c_void_p, ctypes.c_char_p
    api("mono_set_dirs", None, string, string)(str(managed).encode(), str(runtime / "etc").encode())
    api("mono_set_assemblies_path", None, string)(paths.encode())
    domain = api("mono_jit_init_version", ptr, string, string)(b"StaffingChecks", b"v4.0.30319")
    argv = [str(exe).encode(), *(arg.encode() for arg in arguments)]
    assembly = api("mono_domain_assembly_open", ptr, ptr, string)(domain, argv[0])
    if not assembly:
        raise RuntimeError("Mono could not load the probe.")
    return api("mono_jit_exec", ctypes.c_int, ptr, ptr, ctypes.c_int, ctypes.POINTER(string))(
        domain, assembly, len(argv), (string * len(argv))(*argv))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game", type=Path, default=Path(r"D:\SteamLibrary\steamapps\common\Super Fantasy Kingdom"))
    parser.add_argument("--profile", type=Path, default=Path(os.environ["APPDATA"]) /
                        "Thunderstore Mod Manager/DataFolder/SuperFantasyKingdom/profiles/Worker Presets Dev")
    args = parser.parse_args()
    version = json.loads((ROOT / "thunderstore/manifest.json").read_text())["version_number"]
    game = args.game.resolve()
    core = args.profile.resolve() / "BepInEx/core"
    managed = game / "Super Fantasy Kingdom_Data/Managed"
    with TemporaryDirectory() as temp:
        exe = Path(temp) / "Probe.exe"
        dll = exe.with_name("WorkerPresets.dll")
        with ZipFile(ROOT / "dist" / f"WorkerPresets-{version}.zip") as archive:
            payload = archive.read("plugins/WorkerPresets.dll")
        if payload != (ROOT / "bin/Release/netstandard2.1/WorkerPresets.dll").read_bytes():
            raise ValueError("The ZIP and Release DLLs differ.")
        print("Testing packaged DLL SHA-256", hashlib.sha256(payload).hexdigest(), flush=True)
        dll.write_bytes(payload)
        refs = [managed / name for name in ("Assembly-CSharp.dll", "UnityEngine.dll", "UnityEngine.CoreModule.dll",
                                            "UnityEngine.UI.dll", "UnityEngine.UIModule.dll", "netstandard.dll")]
        refs.extend([core / "0Harmony.dll", core / "BepInEx.dll"])
        compiler = Path(os.environ["WINDIR"]) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
        subprocess.run([str(compiler), "/nologo", f"/out:{exe}", *(f"/reference:{path}" for path in refs),
                        str(Path(__file__).with_name("Probe.cs"))], check=True)
        # The runtime keeps the assemblies mapped until its process exits.
        command = [sys.executable, str(Path(__file__).resolve()), "--worker", str(game), str(core), str(exe), str(dll)]
        return subprocess.run(command, cwd=temp).returncode


if __name__ == "__main__":
    if sys.argv[1:2] == ["--worker"]:
        sys.exit(run_mono(*map(Path, sys.argv[2:5]), sys.argv[5:]))
    sys.exit(main())
