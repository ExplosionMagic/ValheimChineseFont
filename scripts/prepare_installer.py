#!/usr/bin/env python3
"""Synchronize installer payloads and reject stale versioned artifacts."""

from __future__ import annotations

import json
from pathlib import Path
import re
import shutil


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "installer" / "assets"

FILES = {
    ROOT / "artifacts" / "ValheimVietnameseFont.dll": ASSETS / "ValheimVietnameseFont.dll",
    ROOT / "translations" / "Vietnamese" / "ValheimVietHoa.json": ASSETS / "ValheimVietHoa.json",
    ROOT / "SVN-Norse Regular.otf": ASSETS / "SVN-Norse Regular.otf",
    ROOT / "SVN-Norse Bold.otf": ASSETS / "SVN-Norse Bold.otf",
    ROOT / "ValheimVN-Sans-Regular.ttf": ASSETS / "ValheimVN-Sans-Regular.ttf",
    ROOT / "ValheimVN-Serif-Regular.ttf": ASSETS / "ValheimVN-Serif-Regular.ttf",
    ROOT / "licenses" / "OFL-Averia.txt": ASSETS / "OFL-Averia.txt",
    ROOT / "licenses" / "OFL-Noto.txt": ASSETS / "OFL-Noto.txt",
}


def main() -> int:
    manifest = json.loads((ROOT / "thunderstore" / "manifest.json").read_text(encoding="utf-8"))
    vanilla = json.loads((ASSETS / "vanilla-manifest.json").read_text(encoding="utf-8"))
    plugin_source = (ROOT / "font-patch" / "VietnameseFontPlugin.cs").read_text(encoding="utf-8")
    match = re.search(r'PluginVersion = "([^"]+)"', plugin_source)
    versions = {manifest["version_number"], vanilla["version"], match.group(1) if match else None}
    if len(versions) != 1:
        raise ValueError(f"Version mismatch: {versions}")
    ASSETS.mkdir(parents=True, exist_ok=True)
    for source, destination in FILES.items():
        shutil.copy2(source, destination)
    translations = json.loads((ASSETS / "ValheimVietHoa.json").read_text(encoding="utf-8"))
    if len(translations) != vanilla["translation_keys"] + 1:
        raise ValueError(
            f"Translation count mismatch: {len(translations)} != {vanilla['translation_keys']} + language label"
        )
    print(f"Installer payload synchronized for {manifest['version_number']}: {len(translations)} JSON entries")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
