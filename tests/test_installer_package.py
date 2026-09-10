import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_installer_payload_is_complete_and_versioned() -> None:
    manifest = json.loads((ROOT / "thunderstore" / "manifest.json").read_text(encoding="utf-8"))
    vanilla = json.loads((ROOT / "installer/assets/vanilla-manifest.json").read_text(encoding="utf-8"))
    source = (ROOT / "font-patch/VietnameseFontPlugin.cs").read_text(encoding="utf-8")
    assert manifest["version_number"] == vanilla["version"] == "0.3.0"
    assert 'PluginVersion = "0.3.0"' in source
    assert manifest["dependencies"] == ["denikson-BepInExPack_Valheim-5.4.2333"]
    patch = ROOT / "installer/assets/resources.assets.bsdiff"
    assert patch.is_file()
    assert patch.stat().st_size < 1_000_000


def test_installer_translation_matches_canonical_json() -> None:
    canonical = (ROOT / "translations/Vietnamese/ValheimVietHoa.json").read_bytes()
    embedded = (ROOT / "installer/assets/ValheimVietHoa.json").read_bytes()
    assert hashlib.sha256(canonical).digest() == hashlib.sha256(embedded).digest()


def test_installer_dll_matches_release_artifact() -> None:
    release = (ROOT / "artifacts/ValheimVietnameseFont.dll").read_bytes()
    embedded = (ROOT / "installer/assets/ValheimVietnameseFont.dll").read_bytes()
    assert hashlib.sha256(release).digest() == hashlib.sha256(embedded).digest()
