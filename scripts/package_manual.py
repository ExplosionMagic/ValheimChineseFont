#!/usr/bin/env python3
"""Build a ZIP that can be extracted directly into the Valheim game directory."""

from __future__ import annotations

import json
import os
from pathlib import Path
import tempfile
from zipfile import ZIP_DEFLATED, ZipFile


ROOT = Path(__file__).resolve().parents[1]
PLUGIN_ROOT = "BepInEx/plugins/ValheimVietHoa"


def main() -> int:
    manifest = json.loads((ROOT / "thunderstore" / "manifest.json").read_text(encoding="utf-8"))
    members = {
        ROOT / "artifacts" / "ValheimVietnameseFont.dll": f"{PLUGIN_ROOT}/ValheimVietnameseFont.dll",
        ROOT / "translations" / "Vietnamese" / "ValheimVietHoa.json": (
            f"{PLUGIN_ROOT}/Translations/Vietnamese/ValheimVietHoa.json"
        ),
        ROOT / "SVN-Norse Regular.otf": f"{PLUGIN_ROOT}/SVN-Norse Regular.otf",
        ROOT / "SVN-Norse Bold.otf": f"{PLUGIN_ROOT}/SVN-Norse Bold.otf",
        ROOT / "ValheimVN-Sans-Regular.ttf": f"{PLUGIN_ROOT}/ValheimVN-Sans-Regular.ttf",
        ROOT / "ValheimVN-Serif-Regular.ttf": f"{PLUGIN_ROOT}/ValheimVN-Serif-Regular.ttf",
        ROOT / "licenses" / "OFL-Averia.txt": f"{PLUGIN_ROOT}/OFL-Averia.txt",
        ROOT / "licenses" / "OFL-Noto.txt": f"{PLUGIN_ROOT}/OFL-Noto.txt",
        ROOT / "docs" / "CAI_DAT_THU_CONG.md": "HUONG-DAN-CAI-DAT.md",
    }
    missing = [str(path) for path in members if not path.is_file()]
    if missing:
        raise FileNotFoundError("Missing manual-package files:\n" + "\n".join(missing))
    output_dir = ROOT / "dist" / "manual"
    output_dir.mkdir(parents=True, exist_ok=True)
    output = output_dir / f"ValheimVietHoa-Mod-{manifest['version_number']}.zip"
    descriptor, temporary = tempfile.mkstemp(prefix=output.stem, suffix=".zip", dir=output_dir)
    os.close(descriptor)
    temporary_path = Path(temporary)
    try:
        with ZipFile(temporary_path, "w", ZIP_DEFLATED, compresslevel=9) as archive:
            for source, destination in members.items():
                archive.write(source, destination)
        os.replace(temporary_path, output)
    finally:
        temporary_path.unlink(missing_ok=True)
    print(f"Built {output} ({len(members)} files)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
