# Publishing to Thunderstore

## First release

1. Run `uv sync --extra dev` and `uv run pytest -q`.
2. Build the upload archive with `python3 scripts/package_thunderstore.py`.
3. Inspect the ZIP in `dist/thunderstore/`. Its root must contain `manifest.json`, `README.md`,
   `CHANGELOG.md`, and the 256×256 `icon.png`. The mod payload must be under
   `plugins/ValheimVietHoa/` so mod managers preserve the bundled loader's translation folder.
4. Sign in to Thunderstore and create or select the team that will permanently own the package.
5. Open the Valheim community upload page and upload the ZIP.
6. Select appropriate categories such as `Mods`, `Client-side`, `Language`, and `AI Generated`.
7. Review the rendered README, dependency list, package namespace, and version before publishing.

The Thunderstore team name becomes part of the permanent dependency string. Pick it carefully.

## Updates

1. Update `PluginVersion` in `font-patch/VietnameseFontPlugin.cs` when the DLL changes.
2. Bump `version_number` in `thunderstore/manifest.json` using semantic versioning.
3. Add release notes to `thunderstore/CHANGELOG.md`.
4. Rebuild and test the package.
5. Tag the matching commit, for example `git tag v0.3.0 && git push origin v0.3.0`.
6. Upload the new ZIP under the same Thunderstore team and package name.

The GitHub release workflow updates the prerelease `main-latest` on every push to `main` and creates
a stable release when a `v*` tag is pushed. It publishes the Thunderstore ZIP, direct-copy mod ZIP,
standalone Windows/macOS/Linux installers, and `SHA256SUMS.txt`.

## Fonts

The public archive includes `SVN-Norse Regular.otf`, `SVN-Norse Bold.otf`,
`ValheimVN-Sans-Regular.ttf`, and `ValheimVN-Serif-Regular.ttf` beside the plugin DLL. The latter two
are renamed, Vietnamese-complete derivatives of Valheim's embedded Averia fonts; eight missing accent
marks come from Noto. They remain under the SIL Open Font License, with the Averia and Noto notices
in `OFL-Averia.txt` and `OFL-Noto.txt`. The packaging command fails if any required font or license
file is missing.

## Vanilla installer payload after a Valheim update

The no-mod patch is deliberately locked to one Steam build. After extracting and translating the
new game version, build a verified target and then regenerate the committed BSDIFF payload:

```bash
uv run valheim-vn build-assets --workspace workspace --output dist/vanilla/resources.assets
uv run python scripts/build_vanilla_patch.py \
  --source "/path/to/Valheim/.../resources.assets" \
  --target dist/vanilla/resources.assets \
  --build-id "STEAM_BUILD_ID"
python3 scripts/prepare_installer.py
```

Commit `installer/assets/resources.assets.bsdiff`, `vanilla-manifest.json`, and synchronized installer
payloads together. The script refuses source/target files that do not match the verified asset-build
report. Never publish one patch for multiple source hashes.
