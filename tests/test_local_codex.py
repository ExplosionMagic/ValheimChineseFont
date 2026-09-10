from pathlib import Path
from types import SimpleNamespace

from pydantic import BaseModel
import pytest

from valheim_vn.cli import build_parser
from valheim_vn.local_codex import _codex_structured, _strict_json_schema


class ExampleResponse(BaseModel):
    answer: str


def test_strict_schema_closes_nested_objects_and_requires_defaulted_fields() -> None:
    schema = _strict_json_schema({
        "type": "object",
        "properties": {
            "item": {
                "type": "object",
                "properties": {"notes": {"type": "string", "default": ""}},
                "required": [],
            }
        },
        "required": ["item"],
    })

    assert schema["additionalProperties"] is False
    assert schema["properties"]["item"]["additionalProperties"] is False
    assert schema["properties"]["item"]["required"] == ["notes"]
    assert "default" not in schema["properties"]["item"]["properties"]["notes"]


def test_local_codex_uses_ephemeral_read_only_structured_exec(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    captured: dict[str, object] = {}

    def fake_run(command: list[str], **kwargs: object) -> SimpleNamespace:
        captured["command"] = command
        captured["kwargs"] = kwargs
        output = Path(command[command.index("--output-last-message") + 1])
        output.write_text('{"answer":"Bản dịch"}', encoding="utf-8")
        return SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr("valheim_vn.local_codex.shutil.which", lambda _: "/usr/bin/codex")
    monkeypatch.setattr("valheim_vn.local_codex.subprocess.run", fake_run)

    parsed = _codex_structured(
        tmp_path, "translate", ExampleResponse, model="test-model", timeout=42
    )

    assert parsed.answer == "Bản dịch"
    command = captured["command"]
    assert isinstance(command, list)
    assert command[:2] == ["/usr/bin/codex", "exec"]
    assert "--ephemeral" in command
    assert command[command.index("--sandbox") + 1] == "read-only"
    assert command[command.index("--model") + 1] == "test-model"
    assert command[-1] == "-"
    assert captured["kwargs"]["input"] == "translate"
    assert captured["kwargs"]["cwd"] == tmp_path


def test_local_codex_requires_installed_cli(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    monkeypatch.setattr("valheim_vn.local_codex.shutil.which", lambda _: None)
    with pytest.raises(RuntimeError, match="not found"):
        _codex_structured(tmp_path, "translate", ExampleResponse)


def test_translate_parser_accepts_local_codex_provider() -> None:
    args = build_parser().parse_args([
        "translate", "--provider", "codex", "--codex-batch-size", "7", "--execute",
    ])

    assert args.provider == "codex"
    assert args.codex_batch_size == 7
    assert args.execute
