#!/usr/bin/env python3
"""Extract TMP Essential Resources into the Unity project.

AssetDatabase.ImportPackage is asynchronous and its completion callback only
fires while the editor loop is pumping. Under `-batchmode -quit` the editor
exits first, so the import logs "Importing..." and then silently does nothing —
which ships an APK where every TextMeshPro Awake() throws NullReferenceException
and all labels render blank. That failure costs a full build cycle to discover.

A .unitypackage is just a gzipped tar of <guid>/{pathname,asset,asset.meta}
entries, so we extract it directly and keep Unity out of the critical path.
Extraction preserves each asset's original GUID via its asset.meta, which
matters: TMP_Settings is referenced by GUID, not by path.

Idempotent — exits 0 immediately if the essentials are already present.
"""

import gzip
import io
import shutil
import sys
import tarfile
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent / "unity"
SENTINEL = PROJECT / "Assets/TextMesh Pro/Resources/TMP Settings.asset"
PACKAGE_NAME = "TMP Essential Resources.unitypackage"


def find_package() -> Path | None:
    """Prefer the project's resolved package, fall back to the editor install."""
    roots = [PROJECT / "Library/PackageCache"]
    roots += [Path(p) for p in sys.argv[1:]]
    roots.append(Path("/Applications/Unity"))
    for root in roots:
        if not root.exists():
            continue
        found = sorted(root.rglob(PACKAGE_NAME))
        if found:
            return found[0]
    return None


def extract(package: Path) -> int:
    """Write every entry to its recorded project-relative pathname."""
    entries: dict[str, dict[str, bytes]] = {}
    with gzip.open(package, "rb") as gz:
        with tarfile.open(fileobj=io.BytesIO(gz.read())) as tar:
            for member in tar.getmembers():
                if not member.isfile():
                    continue
                # Members are named "./<guid>/<kind>" — the leading "." must go,
                # or every entry collapses onto a single key.
                name = member.name.lstrip("./")
                parts = name.split("/")
                if len(parts) < 2:
                    continue
                guid, kind = parts[0], parts[-1]
                if kind not in ("pathname", "asset", "asset.meta"):
                    continue  # preview.png and friends
                handle = tar.extractfile(member)
                if handle is not None:
                    entries.setdefault(guid, {})[kind] = handle.read()

    written = 0
    for guid, parts in entries.items():
        raw = parts.get("pathname")
        if not raw:
            continue
        # pathname may carry a trailing newline and an optional second line.
        rel = raw.decode("utf-8").splitlines()[0].strip()
        if not rel.startswith("Assets/"):
            continue

        dest = PROJECT / rel
        dest.parent.mkdir(parents=True, exist_ok=True)

        if "asset" in parts:
            dest.write_bytes(parts["asset"])
            written += 1
        else:
            dest.mkdir(exist_ok=True)  # folder entry

        if "asset.meta" in parts:
            dest.with_name(dest.name + ".meta").write_bytes(parts["asset.meta"])

    return written


def main() -> int:
    if SENTINEL.exists():
        print(f"[tmp-essentials] already present: {SENTINEL.relative_to(PROJECT)}")
        return 0

    package = find_package()
    if package is None:
        print(f"[tmp-essentials] ERROR: {PACKAGE_NAME} not found.", file=sys.stderr)
        print("  Searched the project's Library/PackageCache and /Applications/Unity.", file=sys.stderr)
        print("  Pass extra search roots as arguments if the editor lives elsewhere.", file=sys.stderr)
        return 1

    print(f"[tmp-essentials] extracting {package}")
    written = extract(package)

    if not SENTINEL.exists():
        print(f"[tmp-essentials] ERROR: extracted {written} files but {SENTINEL.name} is still missing.",
              file=sys.stderr)
        return 1

    print(f"[tmp-essentials] extracted {written} files; sentinel present")
    return 0


if __name__ == "__main__":
    sys.exit(main())
