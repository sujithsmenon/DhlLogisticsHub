# HTTPS Setup (Task 1)

**Date:** 2026-06-10 · **Env:** `DhlLogisticsHub-prod` (EB Windows/IIS, single-instance, **HTTP-only today**) · **Domain:** `pvgt.co.in`

## Implemented in `Program.cs` (middleware section)

```csharp
// HSTS in every non-Development environment. Browsers ignore Strict-Transport-Security
// over plain HTTP, so emitting it now is safe; it activates once TLS is live.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Config-driven HTTPS redirect — default false so the current HTTP env keeps working.
var requireHttps = app.Configuration.GetValue<bool>("Security:RequireHttps");
if (requireHttps)
{
    app.UseHttpsRedirection();
}
```

`app.UseForwardedHeaders()` already runs first (with `KnownNetworks`/`KnownProxies` cleared) so the app trusts `X-Forwarded-Proto` from an upstream ALB/IIS and does not redirect-loop once TLS is terminated upstream.

## Defaults & the RequireHttps flag

| File | `Security:RequireHttps` |
|------|--------------------------|
| `appsettings.json` (base / dev) | `false` |
| `appsettings.Production.json` | **`true`** |

`appsettings.Production.json` now ships **`true`** so the production end-state enforces HTTPS. ⚠️ **The domain currently points at a bare EC2 Elastic IP (`13.234.76.3`) with no TLS listener.** Deploying with `true` *before* TLS is live would `307 → https://pvgt.co.in` to a closed port and **take the site down**.

**Safe sequencing:**
1. Do NOT deploy `appsettings.Production.json` until TLS is live — **or** keep it in the bundle but override at runtime with EB env var **`Security__RequireHttps=false`** (env vars beat JSON).
2. Provision TLS (`PVGT_TLS_IMPLEMENTATION.md`).
3. Remove the override (or set `Security__RequireHttps=true`) → enforcement active.

This is flagged directly in `appsettings.Production.json`'s `_comment`.

## Why this is safe to ship now
- `UseHsts()` over HTTP is a no-op in browsers → no risk on the current HTTP env.
- `UseHttpsRedirection()` is gated off by default → no redirect loop.
- The code is **ready**; enabling HTTPS later is a one-variable change, no redeploy of code.

## Turning HTTPS on (one of two paths)

**Path A — ALB + ACM (recommended):**
1. ACM → request public cert for `pvgt.co.in` (+`www`) in **ap-south-1**, DNS-validate via Route53.
2. EB → change environment type to **Load balanced**; add **HTTPS:443** listener with the cert; redirect 80→443.
3. Route53 → repoint `pvgt.co.in` A-record to an **Alias → ALB** (replacing the EIP A-record).
4. Set EB env var **`Security__RequireHttps=true`**.

**Path B — IIS cert on the single instance:**
1. Issue a cert (win-acme / Let's Encrypt) via `.ebextensions`, or import one.
2. Add an IIS **443 binding** on "Default Web Site"; open 443 in the security group.
3. Set **`Security__RequireHttps=true`**; automate the 90-day renewal.

## Verify after enabling
- `curl -I http://pvgt.co.in/` → 301/307 to `https://…`
- `curl -I https://pvgt.co.in/` → 200 + `Strict-Transport-Security` header
- Blazor circuit reconnects over `wss://`; login + SignalR hubs work; mobile app (now `https://pvgt.co.in`) authenticates.

## Follow-up once TLS is permanent
Harden the Identity cookie (not enabled now — would drop the cookie on HTTP and break login):
```csharp
builder.Services.ConfigureApplicationCookie(o => {
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite     = SameSiteMode.Lax;
});
```
