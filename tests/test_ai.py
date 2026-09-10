import pytest
from pydantic import ValidationError

from valheim_vn.ai import (
    ECONOMY_MODEL, _classification_response_format, _exact_response_format,
    _needs_ai_classification, _run_route,
)
from valheim_vn.schema import SegmentState, Tier, TranslationDecision


def state(segment_id: str, *, status: str = "pending",
          classifier: str | None = None) -> SegmentState:
    context = {} if classifier is None else {"classifier": classifier}
    return SegmentState(
        segment_id=segment_id, key=f"key_{segment_id}", english="English",
        source_hash="hash", family="family", domain="general",
        tier=Tier.BASIC, status=status, context=context,
    )


def test_economy_route_is_one_phrase_and_luna_only() -> None:
    route = _run_route(Tier.ULTRA, economy=True)

    assert route.model == ECONOMY_MODEL == "gpt-5.6-luna"
    assert route.review_model == ECONOMY_MODEL
    assert route.batch_size == 1
    assert route.effort == "none"
    assert route.review_effort == "none"


def test_quality_route_is_unchanged() -> None:
    route = _run_route(Tier.ULTRA, economy=False)

    assert route.model == "gpt-5.6-sol"
    assert route.review_model == "gpt-5.6-sol"
    assert route.batch_size == 2


def test_classification_response_requires_each_exact_segment_id() -> None:
    response_format = _classification_response_format([state("id_one"), state("id_two")])
    decision = {
        "tier": "basic", "domain": "interface", "speaker": None,
        "context_notes": "", "ambiguity_notes": [],
    }

    parsed = response_format.model_validate({"id_one": decision, "id_two": decision})
    assert parsed.model_fields_set == {"id_one", "id_two"}
    with pytest.raises(ValidationError):
        response_format.model_validate({"id_one": decision})
    with pytest.raises(ValidationError):
        response_format.model_validate({
            "id_one": decision, "id_two": decision, "unexpected": decision,
        })


def test_exact_response_contract_applies_to_translation() -> None:
    response_format = _exact_response_format(
        "TranslationResponse", [state("id_one"), state("id_two")], TranslationDecision,
    )
    decision = {
        "translation": "Bản dịch", "confidence": 1, "notes": "",
        "glossary_candidates": [],
    }

    parsed = response_format.model_validate({"id_one": decision, "id_two": decision})
    assert parsed.model_fields_set == {"id_one", "id_two"}
    with pytest.raises(ValidationError):
        response_format.model_validate({"id_one": decision})


def test_ai_classification_resume_skips_ai_checkpoint() -> None:
    assert _needs_ai_classification(state("pending"))
    assert _needs_ai_classification(state("offline", status="classified", classifier="deterministic"))
    assert not _needs_ai_classification(state("done", status="classified", classifier="gpt-test"))
