#!/usr/bin/env python3
"""Build the small, version-locked BSDIFF payload used by the standalone installer."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import tempfile

import bsdiff4


ROOT = Path(__file__).resolve().parents[1]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="original resources.assets")
    parser.add_argument("--target", type=Path, required=True, help="verified Vietnamese resources.assets")
    parser.add_argument("--build-id", required=True, help="Steam build ID")
    parser.add_argument(
        "--output", type=Path, default=ROOT / "installer/assets/resources.assets.bsdiff"
    )
    args = parser.parse_args()
    source = args.source.expanduser().resolve()
    target = args.target.expanduser().resolve()
    output = args.output.expanduser().resolve()
    build_report_path = target.with_suffix(target.suffix + ".manifest.json")
    if not build_report_path.is_file():
        raise FileNotFoundError(f"Missing verified build report: {build_report_path}")
    build_report = json.loads(build_report_path.read_text(encoding="utf-8"))
    if sha256(source) != build_report["source_sha256"]:
        raise ValueError("Source does not match the verified build report")
    if sha256(target) != build_report["output_sha256"]:
        raise ValueError("Target does not match the verified build report")
    release = json.loads((ROOT / "thunderstore/manifest.json").read_text(encoding="utf-8"))
    translations = json.loads(
        (ROOT / "translations/Vietnamese/ValheimVietHoa.json").read_text(encoding="utf-8")
    )
    catalog = [
        json.loads(line)
        for line in (ROOT / "workspace/catalog.jsonl").read_text(encoding="utf-8").splitlines()
        if line
    ]
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=output.parent, delete=False) as temporary:
        temporary_path = Path(temporary.name)
    try:
        bsdiff4.file_diff(str(source), str(target), str(temporary_path))
        temporary_path.replace(output)
    finally:
        temporary_path.unlink(missing_ok=True)
    manifest = {
        "version": release["version_number"],
        "valheim_build_id": str(args.build_id),
        "source_sha256": build_report["source_sha256"],
        "output_sha256": build_report["output_sha256"],
        "source_size": source.stat().st_size,
        "output_size": target.stat().st_size,
        "translation_keys": len(translations) - 1,
        "empty_or_unused_keys": sum(item["baseline_tier"] == "skip" for item in catalog),
    }
    (output.parent / "vanilla-manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(f"Built {output} ({output.stat().st_size} bytes) for Steam build {args.build_id}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
