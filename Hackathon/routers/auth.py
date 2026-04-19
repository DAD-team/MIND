from fastapi import APIRouter, Depends
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from fastapi import HTTPException
from firebase_admin import auth
from controllers.auth_controller import get_or_create_user, update_consent
from models.user import ConsentUpdate

router  = APIRouter(prefix="/auth", tags=["auth"])
_bearer = HTTPBearer()


def get_current_user(credentials: HTTPAuthorizationCredentials = Depends(_bearer)) -> dict:
    try:
        return auth.verify_id_token(credentials.credentials)
    except auth.ExpiredIdTokenError:
        raise HTTPException(status_code=401, detail="Token expired")
    except auth.InvalidIdTokenError:
        raise HTTPException(status_code=401, detail="Invalid token")
    except Exception as e:
        raise HTTPException(status_code=401, detail=str(e))


@router.get("/me")
def me(payload: dict = Depends(get_current_user)):
    return get_or_create_user(payload)


@router.put("/consent")
def change_consent(body: ConsentUpdate, payload: dict = Depends(get_current_user)):
    return update_consent(payload["uid"], body)