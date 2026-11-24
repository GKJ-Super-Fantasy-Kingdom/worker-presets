"""Build the local Thunderstore import archive from a Release DLL."""
import json
import struct
from pathlib import Path
from tempfile import TemporaryDirectory
from xml.etree import ElementTree
from zipfile import ZIP_DEFLATED, ZipFile

root = Path(__file__).resolve().parent
listing = root / "thunderstore"
project = ElementTree.parse(root / "WorkerPresets.csproj")
manifest = json.loads((listing / "manifest.json").read_text(encoding="utf-8-sig"))
name, version = manifest["name"], manifest["version_number"]
if name != "WorkerPresets" or version != project.findtext(".//Version"):
    raise SystemExit("Manifest and project name or version differ.")
framework = project.findtext(".//TargetFramework")
files = {
    "LICENSE": root / "LICENSE",
    "manifest.json": listing / "manifest.json",
    "README.md": listing / "README.md",
    "CHANGELOG.md": root / "CHANGELOG.md",
    "icon.png": listing / "icon.png",
    f"plugins/{name}.dll": root / "bin" / "Release" / framework / f"{name}.dll",
}
icon = files["icon.png"].read_bytes()
if icon[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", icon[16:24]) != (256, 256):
    raise SystemExit("Expected a 256 by 256 PNG icon.")

destination = root / "dist" / f"{name}-{version}.zip"
destination.parent.mkdir(exist_ok=True)
with TemporaryDirectory(dir=destination.parent) as scratch:
    staged = Path(scratch) / destination.name
    with ZipFile(staged, "w", compression=ZIP_DEFLATED) as archive:
        for entry, path in files.items():
            archive.write(path, entry)
    with ZipFile(staged) as archive:
        if archive.testzip() or sorted(archive.namelist()) != sorted(files):
            raise SystemExit("Invalid ZIP structure or checksum.")
        for entry, path in files.items():
            if archive.read(entry) != path.read_bytes():
                raise SystemExit(f"Verification failed for {entry}")
    staged.replace(destination)
print(destination)
