# Release: Customer Invoice Number → Billing Groups

**Release branch:** `master` · **Head:** `229c127`
**Last deployed to production:** `07f06a4` (Phase 2.1 era — *not* the current head)
**Target environment:** Elastic Beanstalk `DhlLogisticsHub-prod` (IIS / Windows Server Core 2025, `ap-south-1`) → https://pvgt.co.in

---

## 1. What ships

17 commits. `CustomerInvoiceNumber` becomes the business reference linking the whole billing
workflow, plus a consolidated Customer Invoice raised over a **Billing Group**.

| Area | Change |
|---|---|
| Billing Group | Virtual — derived on read from `CustomerInvoiceNumber`. Nothing stored to form one; existing records join automatically with no migration or re-save |
| Customer Invoice | New entity with its **own** sequence `CI/26-27/0001`, independent of bill numbers |
| Inheritance | `BillWorkflowHandler.ValidateAsync` is the single authoritative rule — re-reads the reference from the source (JobOrder → AWB → Export) on **every** Create *and* Update |
| Safeguards | A bill can sit on at most one consolidated invoice; a previously-issued bill is **superseded**, never double-billed; groups can never be mixed |
| Duplicate detection | Advisory. Same record across *different* bill types = Information (expected); same record twice within *one* bill type = Warning. Never blocks; audits an override |
| PDF | Consolidated multi-bill invoice on the existing layout + Service Breakdown, Invoice Summary, Linked References, bookmarks |
| Linked Documents | One reusable, permission-scoped panel on Bill / Job / AWB / Export / Invoice screens |
| Search | Billing-group chain, document search, `Ctrl+K`, match highlighting, Advanced filter panel |
| Company Master | Bank/SWIFT/UPI + logo, signature, seal, QR — **stored as bytes in the DB** |
| **Security** | **IDOR on `/invoices/doc/{id}` closed; page-level authorization enforced** |
| **Data integrity** | **Duplicate tax-invoice numbers fixed** (`INV/CB/…` vs `INV/FB/…`) |

---

## 2. Deployment checklist

Run in order. Do not skip step 3.

- [ ] **1. Backup production** (see §4). Confirm the dump is non-empty and lists the business tables.
- [ ] **2. Confirm the working tree is clean and on `229c127`** — `git status` empty, `git log -1`.
- [ ] **3. Apply the pending migration** (see §3). **Nothing else in this release requires it, but the
      Company Master page will throw without it.**
- [ ] **4. Publish + package**
      `dotnet publish DhlLogistics.Web -c Release -o publish`
      then zip the **contents** of `publish/` into `deploy.zip` with **forward-slash** entry separators
      (PowerShell's `ZipFile.CreateFromDirectory` writes backslashes — EB rejects them). A Python
      `zipfile` walk works; verify `aws-windows-deployment-manifest.json` is at the zip **root** and the
      backslash-entry count is **0**.
- [ ] **5. Deploy** — `eb deploy` from the repo root. Watch for `Uploading deploy.zip` (good) rather than
      `Creating application version archive` (means the artifact was not picked up).
- [ ] **6. Health-check**
      - `https://pvgt.co.in/health` → `200 "OK"`
      - `https://pvgt.co.in/api/ping` → `{"ok":true}`
      - `eb status` → `Ready` / `Green`
- [ ] **7. Post-deploy smoke** (see §8) — the startup seeders must run once before Customer Invoices works.
- [ ] **8. Fill in the Company Master** — bank details, UPI, logo, signature, seal, QR. Until this is done
      the invoice PDF prints `Bank Details: -`. This is **data entry, not a defect**.

---

## 3. Database migration checklist

**Production has already applied:**
- `20260714054438_AddCustomerInvoiceBillingGroup`
- `20260714060234_AddCustomerInvoiceNumberToAwbAndExport`

**Pending — one migration:**

```
20260714082431_AddCompanyBrandingAndUpi
```

| | |
|---|---|
| Operations | `ADD COLUMN` ×5 on `CompanyDetails`: `LogoImage`, `SignatureImage`, `SealImage`, `QrCodeImage` (bytea), `UpiId` (varchar) |
| All nullable | **Yes** |
| `AlterColumn` / `DropColumn` / backfill / `UPDATE` | **None** |
| Existing rows modified | **Zero** |
| Rehearsed | Applied to a scratch DB restored from the production backup, from a virgin pre-feature state |

**Apply:**

```powershell
$env:ConnectionStrings__DefaultConnection = "<production connection string>"
dotnet ef database update --project DhlLogistics.Web --context AppDbContext
```

> ⚠️ **`Program.cs` does not call `Database.Migrate()`.** Migrations are **never** auto-applied at startup —
> a deploy alone will not migrate. This must be run explicitly.

**Startup seeders** (idempotent, run automatically on first boot):
- `MenuSeed.EnsureCustomerInvoiceMenuAsync` — adds the *Billing → Customer Invoices* leaf **and repairs its
  `RequiresPermission` flag** if an earlier build seeded it permission-free.
- `PermissionSeed.TopUpNewPageAsync` — grants `bills/customer-invoices` to Admin/Manager/Executive/Viewer.
  **Production's roles were seeded long ago, so without this the page would be unreachable for everyone,
  including Admin.**

---

## 4. Backup procedure

```powershell
# Postgres 17 client tools; connection string read from user-secrets, never echoed.
pg_dump "<production connection string>" --format=custom --no-owner --no-privileges `
        --file=backup_pre_billinggroup_<yyyyMMdd>.dump
```

Validate before proceeding:

```powershell
pg_restore --list backup_<...>.dump   # expect ~800+ TOC entries, ~98 tables
```

Confirm `Bills`, `JobOrders`, `BillCharges`, `AwbShipments`, `ExportJobs`, `Vouchers`, `VoucherLines`
appear as `TABLE DATA`.

> The dump contains **production data**. It is gitignored (`*.dump`) and must never be committed or
> shared. Delete it once the release is confirmed stable.

Supabase's own daily/PITR backups are the preferred restore path; the dump is a belt-and-braces second copy.

---

## 5. Rollback procedure

**Application rollback (fast, safe):**

```powershell
eb deploy --version app-07f0-260714_090616686751     # the last known-good version (07f06a4)
```

The new schema is **purely additive**, so the previous application build runs against it unchanged — it
simply ignores the new columns and the `CustomerInvoices` table. **You do not need to roll the database back
to roll the app back.**

**Database rollback (only if you must):**

```powershell
dotnet ef database update 20260714060234_AddCustomerInvoiceNumberToAwbAndExport `
        --project DhlLogistics.Web --context AppDbContext
```

Drops only what `AddCompanyBrandingAndUpi` added. Any branding images uploaded since the deploy are lost.

**Full restore (last resort):**

```powershell
pg_restore --clean --if-exists --no-owner --no-privileges -d "<connection string>" backup_<...>.dump
```

⚠️ Destroys everything written since the backup. Only for a catastrophic failure.

**Data written by this release is reversible by design:** cancelling a consolidated invoice releases its
bills and reactivates their original invoices. The invoice row survives as cancelled history — nothing is
destroyed.

---

## 6. Release notes (user-facing)

**New**
- **Billing → Customer Invoices** — raise one invoice across every bill sharing a Customer Invoice No.
- **Generate Customer Invoice** popup on the bill lists — pick which bills to include, see live totals, expand
  a bill to read its originating job/shipment (read-only).
- **Duplicate billing warning** — flags an operational record billed twice under the *same* bill type. It is
  advisory: your selection is always authoritative, and an override is recorded in the audit log.
- **Linked Documents** panel on Bills, Jobs, AWB, Export and Customer Invoices — jump between related
  documents without searching again.
- **Universal search**: `Ctrl+K`, matched-text highlighting, related-record chains, PDF search, and an
  **Advanced filter panel** (module, status, customer, branch, date range, bill type, job type).
- **Company Master**: SWIFT, UPI, and uploadable logo / signature / seal / payment-QR — all now printed on the
  invoice PDF.

**Fixed**
- **Duplicate tax-invoice numbers.** A Clearance and a Forwarding bill with the same sequence number produced
  the *same* invoice number (`INV/26-27/0005`). Now `INV/CB/…` and `INV/FB/…`. **See §7 — one existing pair
  needs a business decision.**
- **Invoice PDFs could be downloaded by any logged-in user** by guessing the URL, regardless of role.
- Restricted pages could be reached by typing the URL even when hidden from the menu.
- Non-Admin users hit an infinite redirect loop on `/usermanagement`.
- Operations Dashboard status column is now a colour-filled cell and readable in dark mode.
- Every page showed the title "Operations Dashboard".
- Ledger report crashed on open (EF cyclic include).

**Unchanged (deliberately)**
- Accounting. A/R posts per bill at **approval**, never at invoice issue — consolidating bills onto one
  invoice creates **no** accounting entries. Verified: production ledger unchanged throughout.
- Existing per-bill invoice numbers, and the numbers already issued.
- Records with no Customer Invoice No behave exactly as before.

---

## 7. Known limitations

1. 🔴 **Two production bills already share one tax-invoice number** — `CB/26-27/0005` and `FB/26-27/0005`,
   both issued as `INV/26-27/0005`. The code defect is fixed, but **issued documents were not rewritten**.
   This needs a business decision: credit-note-and-reissue, or an accepted annotation.
2. **Company Master has no bank data** — the invoice prints `Bank Details: -` until it is filled in.
3. **Advanced Search filters apply after each provider's fetch**, so a heavily-filtered search can return
   fewer rows than the per-module limit even when more would match deeper in that module.
4. **Customer Invoice list paging is client-side over a server-filtered set** (capped at 500, with an explicit
   banner when the cap is hit). Filtering and search *are* in SQL; only the paging is not.
5. **Email invoice delivery is not implemented** — the button is present and deliberately disabled.
6. **Export/AWB → Bill → Invoice was verified on a scratch database**, not production, because bill approval
   posts real accounting. The chain is proven; it has simply never run against production data.
7. **Dead-CSS pruning was not done.** Class names are composed at runtime (`st-cell-{slug}`), so a static
   scan cannot tell live from dead — pruning on that signal would break the status badges.
8. **Multiple bank accounts** are not supported (the PDF block is a list, so adding them needs no redesign).

---

## 8. Post-deploy smoke test

1. Sign in. Confirm **Billing → Customer Invoices** appears in the sidebar *(seeder ran)*.
2. Open it. Confirm the page loads rather than bouncing to `/no-permission` *(permission top-up ran)*.
3. Open **Billing → Clearance Bills**, click **Invoice** on an approved bill with a Customer Invoice No.
   Confirm the popup lists the group's bills with live totals.
4. **Preview Invoice** — confirm the PDF opens, shows both invoice numbers, and its grand total equals the
   sum of the selected bills.
5. Open **Operations → Operations Dashboard**. Confirm the Status column is colour-filled.
6. Press `Ctrl+K`, search a Customer Invoice No. Confirm grouped results with related-record chains.
7. **Confirm the ledger is unchanged** — Reports → Trial Balance should match its pre-deploy figures.

---

## 9. Remaining enhancements (not in this release)

- Push the Advanced Search filters into each provider's SQL (removes limitation 3).
- True server-side paging on the Customer Invoice list via a Syncfusion `CustomAdaptor` (limitation 4).
- Email invoice delivery.
- Multiple bank accounts in the Company Master.
- Add `SealNumber`, `BillOfEntry`, `PurchaseOrder`, `SalesOrder`, `PackageMarks`, `NotifyParty`, `MAWB` and
  customer `GSTIN`/`PAN` — requested for search but **not present in the schema**, so unsearchable today.
- A runtime CSS-coverage pass so dead CSS can be pruned safely.
- Replace the `MainLayout` permission guard with `AuthorizeRouteView` + a per-page policy, so authorization is
  enforced by the router rather than by the layout.
