import asyncio
import contextlib
import logging
from typing import List, Optional

from fastapi import APIRouter, Depends, FastAPI, Header, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field

from .config import Ayarlar, VARSAYILAN
from .servis import ServisHatasi, VerificationServisi

AZAMI_GOVDE_BAYT = 256 * 1024

class ClaimIstegi(BaseModel):
    token: str = Field(min_length=4, max_length=64)
    public_key: str = Field(min_length=16, max_length=512)
    assurance_level: str = Field(pattern="^(TPM|SOFTWARE)$")
    scanner_version: str = Field(min_length=1, max_length=32)
    ruleset_version: str = Field(min_length=1, max_length=32)

class HeartbeatIstegi(BaseModel):
    verification_id: str = Field(min_length=8, max_length=64)
    nonce: str = Field(min_length=16, max_length=128)
    signature: str = Field(min_length=16, max_length=512)
    sequence_number: int = Field(ge=1, le=10_000_000)
    scan_stage: str = Field(min_length=1, max_length=64)
    progress: int = Field(ge=0, le=100)
    timestamp: str = Field(min_length=1, max_length=64)

class KanitIstegi(BaseModel):
    rule_id: str = Field(min_length=1, max_length=64)
    rule_version: Optional[str] = Field(default=None, max_length=32)
    evidence_type: str = Field(min_length=1, max_length=64)
    artifact_category: str = Field(default="UNKNOWN", max_length=64)
    confidence: str = Field(default="MEDIUM", max_length=16)
    severity: str = Field(default="MEDIUM", max_length=16)
    currently_present: bool = False

    first_seen: Optional[str] = Field(default=None, max_length=32)
    last_seen: Optional[str] = Field(default=None, max_length=32)

class RaporIstegi(BaseModel):
    verification_id: str = Field(min_length=8, max_length=64)
    nonce: str = Field(min_length=16, max_length=128)
    signature: str = Field(min_length=16, max_length=512)
    scanner_version: str = Field(min_length=1, max_length=32)
    ruleset_version: str = Field(min_length=1, max_length=32)
    overall_result: str = Field(min_length=1, max_length=48)
    report_hash: str = Field(min_length=64, max_length=64)
    report_body: str = Field(max_length=AZAMI_GOVDE_BAYT)
    forensic_coverage: str = Field(pattern="^(GOOD|LIMITED|UNAVAILABLE)$")
    runtime_coverage: str = Field(pattern="^(GOOD|LIMITED|UNAVAILABLE)$")
    evidence: List[KanitIstegi] = Field(default_factory=list, max_length=500)
    timestamp: str = Field(min_length=1, max_length=64)

class OlusturIstegi(BaseModel):
    minecraft_uuid: str = Field(min_length=8, max_length=64)

class OturumIstegi(BaseModel):
    minecraft_uuid: str = Field(min_length=8, max_length=64)
    username: str = Field(min_length=1, max_length=32)
    game_session_id: str = Field(min_length=8, max_length=64)
    game_ip: Optional[str] = Field(default=None, max_length=64)

class AnlikGoruntuIstegi(BaseModel):

    sessions: List[OturumIstegi] = Field(default_factory=list, max_length=2000)

def istemci_ip(request: Request) -> Optional[str]:

    iletilen = request.headers.get("x-forwarded-for")
    if iletilen:
        return iletilen.split(",")[0].strip()
    return request.client.host if request.client else None

def uygulama_olustur(servis: VerificationServisi,
                     ayarlar: Ayarlar = VARSAYILAN,
                     plugin_jetonu: str = "gelistirme-jetonu") -> FastAPI:

    @contextlib.asynccontextmanager
    async def yasam_dongusu(_: FastAPI):
        gorev = None
        if ayarlar.bakim_araligi_saniye > 0:
            gorev = asyncio.create_task(bakim_dongusu())
        try:
            yield
        finally:
            if gorev:
                gorev.cancel()
                with contextlib.suppress(asyncio.CancelledError):
                    await gorev

    async def bakim_dongusu():
\
\
\
\
\

        while True:
            await asyncio.sleep(ayarlar.bakim_araligi_saniye)
            try:
                servis.bakim()
                servis.nonce.temizle()
                yaz = getattr(servis.depo, "yaz", None)
                if yaz:
                    yaz()
            except Exception:
                logging.getLogger("hapiverify").exception("bakim turu basarisiz")

    app = FastAPI(title="HapiVerify API", version="0.1.0", lifespan=yasam_dongusu)

    @app.exception_handler(ServisHatasi)
    async def servis_hatasi(request: Request, hata: ServisHatasi):

        return JSONResponse(status_code=hata.http,
                            content={"error": hata.kod, "message": hata.mesaj})

    @app.middleware("http")
    async def kalici_yaz(request: Request, call_next):
\
\
\
\
\

        try:
            return await call_next(request)
        finally:
            yaz = getattr(servis.depo, "yaz", None)
            if yaz:
                yaz()

    @app.middleware("http")
    async def govde_siniri(request: Request, call_next):
        uzunluk = request.headers.get("content-length")
        if uzunluk and int(uzunluk) > AZAMI_GOVDE_BAYT:
            return JSONResponse(status_code=413,
                                content={"error": "BODY_TOO_LARGE",
                                         "message": "istek govdesi cok buyuk"})
        return await call_next(request)

    def plugin_yetkisi(x_plugin_token: str = Header(default="")):

        import hmac
        if not hmac.compare_digest(x_plugin_token, plugin_jetonu):
            raise ServisHatasi("UNAUTHORIZED", "yetkisiz", 401)

    plugin = APIRouter(prefix="/api/v1/plugin", dependencies=[Depends(plugin_yetkisi)])
    scanner = APIRouter(prefix="/api/v1/scanner")

    def besleme():

        saglayici = servis.oturumlar
        if not hasattr(saglayici, "anlik_goruntu"):
            raise ServisHatasi("SESSION_FEED_UNSUPPORTED",
                               "bu backend oturum beslemesi kabul etmiyor", 501)
        return saglayici

    @plugin.put("/sessions")
    async def oturum_goruntusu(istek: AnlikGoruntuIstegi):
\
\
\
\

        sayi = besleme().anlik_goruntu([o.model_dump() for o in istek.sessions])
        return {"online": sayi}

    @plugin.post("/sessions")
    async def oturum_katildi(istek: OturumIstegi):
        besleme().katildi(istek.minecraft_uuid, istek.username,
                          istek.game_session_id, istek.game_ip)
        return {"accepted": True}

    @plugin.delete("/sessions/{minecraft_uuid}")
    async def oturum_ayrildi(minecraft_uuid: str, game_session_id: Optional[str] = None):
        kapandi = besleme().ayrildi(minecraft_uuid, game_session_id)
        return {"closed": kapandi}

    @plugin.post("/verifications")
    async def olustur(istek: OlusturIstegi):
        v, token = servis.olustur(istek.minecraft_uuid)

        return {"verification_id": v.verification_id, "token": token,
                "expires_at": v.expires_at.isoformat(),
                "ttl_seconds": ayarlar.token_ttl_saniye}

    @plugin.get("/verifications/{minecraft_uuid}")
    async def durum(minecraft_uuid: str):
        v = servis.durum(minecraft_uuid)
        if v is None:
            raise ServisHatasi("NO_VERIFICATION", "kayit yok", 404)
        rapor = servis.depo.verification_raporu(v.verification_id)
        return {
            "verification_id": v.verification_id,
            "status": v.status.value,
            "network_status": v.ag_durumu.value,
            "new_device": None,
            "result": rapor.overall_result.value if rapor else None,
        }

    @plugin.delete("/verifications/{minecraft_uuid}")
    async def iptal(minecraft_uuid: str):
        v = servis.iptal(minecraft_uuid)
        return {"verification_id": v.verification_id, "status": v.status.value}

    @scanner.post("/claim")
    async def claim(istek: ClaimIstegi, request: Request):
        return servis.claim(istek.token, istek.public_key, istek.assurance_level,
                            istek.scanner_version, istek.ruleset_version,
                            istemci_ip(request))

    @scanner.post("/heartbeat")
    async def heartbeat(istek: HeartbeatIstegi):
        return servis.heartbeat(istek.verification_id, istek.nonce, istek.signature,
                                istek.sequence_number, istek.scan_stage,
                                istek.progress, istek.timestamp)

    @scanner.post("/report")
    async def rapor(istek: RaporIstegi):
        r = servis.rapor_tamamla(
            istek.verification_id, istek.nonce, istek.signature,
            istek.scanner_version, istek.ruleset_version, istek.overall_result,
            istek.report_hash, istek.report_body, istek.forensic_coverage,
            istek.runtime_coverage, [k.model_dump() for k in istek.evidence],
            istek.timestamp,
        )

        return {"report_id": r.report_id, "accepted": True}

    @app.get("/health")
    async def health():
        return {"status": "ok"}

    app.include_router(plugin)
    app.include_router(scanner)
    return app
