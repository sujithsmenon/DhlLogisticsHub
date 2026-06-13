# Mobile API Validation (Phase 6)

**Date:** 2026-06-10 · Project: `DhlLogistics.Mobile` (.NET MAUI Blazor Hybrid)

## Production endpoint — verified ✅
`DhlLogistics.Mobile/AppConfig.cs:18`
```csharp
public const string ApiBaseUrl = "https://pvgt.co.in";
```
- Previously the decommissioned `https://dhl-logistics-hub.onrender.com` — now correctly the production domain.
- Consumed by:
  - `MauiProgram.cs:34` — `HttpClient.BaseAddress = new Uri(AppConfig.ApiBaseUrl)` (single shared client, 60s timeout).
  - `Services/PushService.cs:54` — SignalR hub `"{ApiBaseUrl}/notificationhub"`.
  - `Platforms/Android/LocationForegroundService.cs:59` — background GPS client `BaseAddress = AppConfig.ApiBaseUrl`.

## Endpoints the app calls (all relative to `https://pvgt.co.in`)
| Path | Verb | Server map | Auth |
|------|------|-----------|------|
| `/api/auth/login` | POST | `AuthEndpoints` | anonymous → returns JWT |
| `/api/ping` | GET | warm-up | anonymous |
| `/api/jobs`, `/api/jobs/{id}`, `/api/jobs/{id}/status` | GET/PUT | `JobEndpoints` | Bearer |
| `/api/dashboard`, `/api/dashboard/{users,vehicles,cargo}` | GET | `DashboardEndpoints` | Bearer |
| `/api/admin/reports/{daily,weekly,monthly}` | GET | `AdminEndpoints` | Bearer (Admin/Manager → 403 otherwise) |
| `/notificationhub` | WS | `NotificationHub` | Bearer |

## Auth flow — verified ✅
- `AuthService.LoginAsync` POSTs to `/api/auth/login`, stores the JWT in `SecureStorage`, and sets `Authorization: Bearer <token>` on the shared `HttpClient` (`AuthService.cs:23,79`).
- Session restored on launch from `SecureStorage` (`TryRestoreSessionAsync`).
- ⚠️ **Dependency on F1 (JWT key):** because tokens are signed with `Jwt:Key`, the mobile auth boundary is only as strong as that key. Until `Jwt__Key` is set to a strong secret server-side, mobile sessions are forgeable. See `JWT_CONFIGURATION.md`.

## TLS dependency — BLOCKING for mobile
The base URL is **HTTPS**. `HttpClient` and SignalR will **fail to connect** until a valid TLS certificate is live on `pvgt.co.in` (see `PVGT_TLS_IMPLEMENTATION.md`). Sequence:
1. Enable TLS on `pvgt.co.in` (ACM + ALB).
2. Smoke-test `https://pvgt.co.in/api/ping` from a device/browser.
3. **Then** build & ship the mobile app.

## CORS — OK
Server applies `AllowAll` (`AllowAnyOrigin/Method/Header`), so the MAUI WebView origin is permitted. No credentials are combined with `AllowAnyOrigin`, so it is safe.

## Minor (non-blocking) cleanup recommendations
- Stale "Render free-tier" comments remain in `MauiProgram.cs:28` and `ApiService.cs:13-15` (functional code is correct). Optional: update the wording to reflect AWS/EB. Left unchanged to keep this pass minimal.

## Verdict
Mobile production endpoint configuration is **correct**. Two external dependencies gate a working mobile release: **TLS on pvgt.co.in** and a **strong `Jwt__Key`**.
