from fastapi import APIRouter, Depends, Query
from routers.auth import get_current_user
from models.journal import JournalCreate, JournalUpdate
from controllers.journal_controller import (
    create_journal, list_journals, get_journal, update_journal, delete_journal,
)

router = APIRouter(prefix="/journals", tags=["journals"])


@router.post("", status_code=201)
def post_journal(body: JournalCreate, user: dict = Depends(get_current_user)):
    return create_journal(user["uid"], body)


@router.get("")
def get_journals(
    limit: int = Query(default=20, ge=1, le=100),
    user: dict = Depends(get_current_user),
):
    return list_journals(user["uid"], limit)


@router.get("/{journal_id}")
def get_journal_by_id(journal_id: str, user: dict = Depends(get_current_user)):
    return get_journal(user["uid"], journal_id)


@router.patch("/{journal_id}")
def patch_journal(journal_id: str, body: JournalUpdate, user: dict = Depends(get_current_user)):
    return update_journal(user["uid"], journal_id, body)


@router.delete("/{journal_id}", status_code=204)
def delete_journal_by_id(journal_id: str, user: dict = Depends(get_current_user)):
    delete_journal(user["uid"], journal_id)
