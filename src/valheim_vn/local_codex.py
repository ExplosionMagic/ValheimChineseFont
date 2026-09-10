from __future__ import annotations

import json
from pathlib import Path
import shutil
import subprocess
import tempfile

from pydantic import BaseModel

from .ai import (
    REVIEW_PROMPT,
    SYSTEM_PROMPT,
    _catalog_map,
    _chunks,
    _exact_response_format,
    _glossary_context,
    _progress,
    _quoted,
    _translation_payload,
)
from .memory import TranslationMemory
from .schema import ReviewDecision, SegmentState, Tier, TranslationDecision
from .validation import locked_glossary_errors, validate_translation


def _strict_json_schema(node: object) -> object:
    """Make Pydantic JSON Schema acceptable to strict structured outputs."""
    if isinstance(node, list):
        return [_strict_json_schema(item) for item in node]
    if not isinstance(node, dict):
        return node
    strict = {key: _strict_json_schema(value) for key, value in node.items() if key != "default"}
    if strict.get("type") == "object" or "properties" in strict:
        strict["additionalProperties"] = False
        properties = strict.get("properties")
        if isinstance(properties, dict):
            strict["required"] = list(properties)
    return strict


def _codex_structured(
    workspace: Path,
    prompt: str,
    response_format: type[BaseModel],
    *,
    model: str | None = None,
    timeout: float = 1800.0,
) -> BaseModel:
    executable = shutil.which("codex")
    if executable is None:
        raise RuntimeError("Local Codex CLI was not found on PATH")
    with tempfile.TemporaryDirectory(prefix="valheim-vn-codex-") as temporary:
        directory = Path(temporary)
        schema_path = directory / "response.schema.json"
        output_path = directory / "response.json"
        schema_path.write_text(
            json.dumps(_strict_json_schema(response_format.model_json_schema()), ensure_ascii=False),
            encoding="utf-8",
        )
        command = [
            executable,
            "exec",
            "--ephemeral",
            "--sandbox",
            "read-only",
            "--skip-git-repo-check",
            "--color",
            "never",
            "--output-schema",
            str(schema_path),
            "--output-last-message",
            str(output_path),
        ]
        if model:
            command.extend(("--model", model))
        command.append("-")
        try:
            completed = subprocess.run(
                command,
                input=prompt,
                text=True,
                capture_output=True,
                cwd=workspace,
                timeout=timeout,
                check=False,
            )
        except subprocess.TimeoutExpired as exc:
            raise RuntimeError(f"Local Codex timed out after {timeout:g} seconds") from exc
        if completed.returncode != 0:
            detail = (completed.stderr or completed.stdout).strip()[-1500:]
            raise RuntimeError(
                f"Local Codex exited with status {completed.returncode}"
                + (f": {detail}" if detail else "")
            )
        if not output_path.is_file():
            raise RuntimeError("Local Codex did not write its structured response")
        try:
            return response_format.model_validate_json(output_path.read_text(encoding="utf-8"))
        except ValueError as exc:
            raise RuntimeError("Local Codex returned invalid structured output") from exc


def _require_exact_ids(parsed: BaseModel, states: list[SegmentState], actor: str) -> None:
    expected = {state.segment_id for state in states}
    if parsed.model_fields_set != expected:
        raise RuntimeError(f"Local Codex {actor} returned missing or unexpected segment IDs")


def translate_segments_codex(
    workspace: Path,
    tiers: tuple[Tier, ...] | None = None,
    limit: int | None = None,
    max_invocations: int | None = None,
    context_limit: int = 8,
    batch_size: int = 12,
    model: str | None = None,
    review: bool = True,
    timeout: float = 1800.0,
    progress: bool = True,
) -> dict[str, int]:
    if context_limit < 0:
        raise ValueError("context_limit must be zero or greater")
    if batch_size < 1:
        raise ValueError("Codex batch size must be at least one")
    if timeout <= 0:
        raise ValueError("Codex timeout must be greater than zero")
    catalog = _catalog_map(workspace)
    invocations = 0
    with TranslationMemory(workspace / "memory.sqlite3") as memory:
        states = memory.select(("classified", "failed", "needs_review"), tiers=tiers, limit=limit)
        states = [state for state in states if state.tier != Tier.SKIP]
        total = len(states)
        _progress(
            progress,
            f"RUN phrases={total} provider=codex batch_size={batch_size} "
            f"review={'on' if review else 'off'} max_invocations={max_invocations or 'unlimited'}",
        )
        completed_count = 0
        for batch in _chunks(states, batch_size):
            calls_needed = 1 + int(review)
            if max_invocations is not None and invocations + calls_needed > max_invocations:
                _progress(
                    progress,
                    f"STOP local invocation ceiling reached: invocations={invocations}, "
                    f"completed={completed_count}/{total}",
                )
                return memory.stats()
            for offset, state in enumerate(batch):
                _progress(
                    progress,
                    f"[{completed_count + offset + 1}/{total}] START key={state.key} "
                    f"tier={state.tier.value} provider=codex",
                )
                _progress(progress, f"  EN: {_quoted(state.english)}")
            payload = _translation_payload(memory, catalog, batch, context_limit)
            translation_format = _exact_response_format(
                "LocalCodexTranslationResponse", batch, TranslationDecision
            )
            parsed = _codex_structured(
                workspace,
                SYSTEM_PROMPT
                + "\n\nTranslate every segment in the payload. Deliberately check your own work once "
                  "before returning only the schema-conforming JSON object. Do not call tools or edit files.\n\n"
                + json.dumps(payload, ensure_ascii=False),
                translation_format,
                model=model,
                timeout=timeout,
            )
            invocations += 1
            _require_exact_ids(parsed, batch, "translator")
            candidates = {
                state.segment_id: getattr(parsed, state.segment_id)
                for state in batch
            }
            for state in batch:
                candidate = candidates[state.segment_id]
                _progress(
                    progress,
                    f"  CANDIDATE confidence={candidate.confidence:.2f}: "
                    f"{_quoted(candidate.translation)}",
                )

            reviews: dict[str, ReviewDecision] = {}
            if review:
                review_payload = _translation_payload(memory, catalog, batch, context_limit)
                review_payload["candidates"] = {
                    state.segment_id: candidates[state.segment_id].translation
                    for state in batch
                }
                review_format = _exact_response_format(
                    "LocalCodexReviewResponse", batch, ReviewDecision
                )
                reviewed = _codex_structured(
                    workspace,
                    SYSTEM_PROMPT
                    + "\n\n"
                    + REVIEW_PROMPT
                    + "\nThis is a fresh review pass. Return only the schema-conforming JSON object. "
                      "Do not call tools or edit files.\n\n"
                    + json.dumps(review_payload, ensure_ascii=False),
                    review_format,
                    model=model,
                    timeout=timeout,
                )
                invocations += 1
                _require_exact_ids(reviewed, batch, "reviewer")
                reviews = {
                    state.segment_id: getattr(reviewed, state.segment_id)
                    for state in batch
                }

            glossary = _glossary_context(memory, batch)
            for state in batch:
                candidate = candidates[state.segment_id]
                translation = candidate.translation.strip()
                notes = [candidate.notes]
                rejected = False
                if review:
                    decision = reviews[state.segment_id]
                    notes.extend(decision.issues)
                    if decision.verdict == "revise" and decision.revised_translation:
                        translation = decision.revised_translation.strip()
                    elif decision.verdict == "reject":
                        rejected = True
                result = validate_translation(state.english, translation)
                errors = list(result.errors)
                errors.extend(locked_glossary_errors(state.english, translation, glossary))
                if rejected:
                    errors.append("semantic reviewer rejected the candidate")
                status = "failed" if errors else "approved"
                memory.save_translation(
                    state.segment_id,
                    translation,
                    status,
                    f"codex-cli:{model or 'default'}",
                    "local",
                    "; ".join(filter(None, (*notes, *result.warnings))),
                    errors,
                )
                _progress(progress, f"  RESULT status={status} VI={_quoted(translation)}")
                if errors:
                    _progress(progress, f"  ERRORS: {_quoted('; '.join(errors))}")
            completed_count += len(batch)
            _progress(
                progress,
                f"PROGRESS completed={completed_count}/{total} invocations={invocations}",
            )
        return memory.stats()
