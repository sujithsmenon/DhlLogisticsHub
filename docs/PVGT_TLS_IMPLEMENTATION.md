# pvgt.co.in TLS Implementation

**Date:** 2026-06-10 · **No AWS resources are modified by this document — it is the runbook.**

Current state: `pvgt.co.in` A-record → `13.234.76.3` (bare EC2 Elastic IP on a single-instance EB env), **HTTP only**. ACM certs cannot attach to a bare EC2 instance, so the recommended path adds an Application Load Balancer.

> The app is already TLS-ready: `UseForwardedHeaders` (proxy-aware), `UseHsts()` in non-Dev, and `UseHttpsRedirection()` gated on `Security:RequireHttps`. Nothing in code changes for this — only AWS + one config flag.

## 1. Create the ACM certificate
- Console: **ACM → Request certificate → Public**, **region ap-south-1** (must match the ALB region).
- Domain names: `pvgt.co.in` **and** `www.pvgt.co.in`.
- Validation method: **DNS validation**.
- CLI equivalent:
  ```bash
  aws acm request-certificate \
    --domain-name pvgt.co.in \
    --subject-alternative-names www.pvgt.co.in \
    --validation-method DNS \
    --region ap-south-1
  ```

## 2. Validation records (Route53)
- In ACM, the cert shows a **CNAME** record per domain. Click **"Create records in Route53"** to auto-insert them into the existing `pvgt.co.in` hosted zone.
- Wait until certificate status = **Issued** (usually minutes).
  ```bash
  aws acm describe-certificate --certificate-arn <ARN> --region ap-south-1 \
    --query "Certificate.Status"
  ```

## 3. Load Balancer + HTTPS listener
- EB → environment → **Configuration → Capacity** → Environment type = **Load balanced** (adds an ALB).
- **Configuration → Load balancer**:
  - Add listener **Port 443 / HTTPS**, SSL certificate = the ACM cert from step 1, policy `ELBSecurityPolicy-TLS13-1-2-2021-06` (or newer).
  - Keep listener **Port 80 / HTTP**; add a rule to **redirect 80 → 443** (or let the app's `UseHttpsRedirection` do it once the flag is on).
- Ensure the instance security group allows the ALB on the app port; the ALB SG allows 80/443 from the internet.
- Target group **health check path = `/health`**.

## 4. Route53 Alias
- Replace the existing `pvgt.co.in` **A → 13.234.76.3** record with an **A record, Alias = Yes → the EB ALB** DNS name.
- Add `www.pvgt.co.in` as an Alias → ALB too (or a redirect rule www→apex).
  ```bash
  # (illustrative — use the ALB's hosted-zone id + DNS name from describe-load-balancers)
  aws route53 change-resource-record-sets --hosted-zone-id <ZONE_ID> --change-batch '{
    "Changes":[{"Action":"UPSERT","ResourceRecordSet":{
      "Name":"pvgt.co.in","Type":"A",
      "AliasTarget":{"HostedZoneId":"<ALB_ZONE_ID>","DNSName":"<ALB_DNS_NAME>","EvaluateTargetHealth":true}
    }}]
  }'
  ```

## 5. Enable enforcement in the app
- Set EB env var **`Security__RequireHttps=true`** (or rely on `appsettings.Production.json`, which already ships `true`).
  - `UseHsts()` is already active in production; `UseHttpsRedirection()` now activates and 80→443 is enforced end-to-end.
- The environment restarts after the env-var change.

## 6. Validation
```bash
curl -I http://pvgt.co.in/        # expect 301/307 → https://pvgt.co.in/
curl -I https://pvgt.co.in/       # expect 200 + Strict-Transport-Security header
curl -I https://www.pvgt.co.in/   # expect 200 (or 301 → apex)
openssl s_client -connect pvgt.co.in:443 -servername pvgt.co.in </dev/null 2>/dev/null | openssl x509 -noout -subject -dates
```
- Browser: padlock valid; Blazor circuit reconnects over `wss://`; login + SignalR `/gpshub` `/notificationhub` work.
- Mobile app (`https://pvgt.co.in`) authenticates; notification hub connects.

## Rollback
- If the site breaks after enabling, set EB env `Security__RequireHttps=false` (immediate, no redeploy) to stop redirecting while you fix the listener/cert.

## After TLS is permanent — harden cookies (follow-up)
```csharp
builder.Services.ConfigureApplicationCookie(o => {
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite     = SameSiteMode.Lax;
});
```
Not enabled now because `Always` drops the cookie over HTTP and breaks login pre-TLS.
