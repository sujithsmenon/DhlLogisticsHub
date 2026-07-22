# AI Email Automation — Phase 6: Existing Billing / Accounting / GST (Verification)

**Goal of this phase:** confirm that a shipment/job created by the AI email
pipeline (Phases 1–5) flows into the **existing** billing, transportation,
accounting, voucher, GST and customer-invoice modules **without any redesign and
without new billing code**, and that nothing bypasses the existing approval gates.

**Result: no billing code was added or changed in Phase 6.** The auto-created
`JobOrder` and shipments are ordinary rows that the existing services already
handle. This document records the trace and a manual end-to-end test.

---

## 1. How the auto-created job reaches billing (code trace)

Phase 5 creates the Clearing/Forwarding job through the **same** entry point a
human uses — `JobOrderService.CreateAsync` — which runs the shared
`WorkflowOrchestrator`. Nothing about the billing path is special-cased for the
AI pipeline.

| Step | Where | Behaviour |
|------|-------|-----------|
| Job created | `JobOrderWorkflowHandler` (Create) | Status set to **Submitted** (`JobOrderWorkflowHandler.cs:101`). Enters the normal Verify → Approve flow. |
| Billing gate | `JobOrderWorkflowHandler.GenerateBillingAsync` | **Returns early unless `job.Status == Approved`** (`:203`). A Submitted/Verified job raises **no** bill. |
| On approval | same method | `BillService.UpsertForJobAsync(job)` + `AccountingService.PostJobExpensesAsync(job, user)` (`:205–206`), inside the orchestrator transaction. |
| Bill carries master ref | `BillService.UpsertForJobAsync` | `Bill.CustomerInvoiceNumber = job.CustomerInvoiceNumber` (`BillService.cs:97`, kept in sync `:257`). |
| Accounting / GST | `AccountingService.PostForBillAsync` / `PostJobExpensesAsync` | Existing double-entry (A/R, Revenue, GST output, vendor expense/payable). Unchanged. |
| Transportation bill | `TransportationBillService.PrepareFromAwbAsync` / `PrepareFromExportAsync` | Existing TB path off the Phase-4 AWB / Sea shipment. Unchanged. |
| Customer invoice | `InvoiceService` / `CustomerInvoiceService` | Existing issue-from-approved-bill path. Unchanged. |

### Why nothing bypasses approval
The pipeline has **three** human/gated checkpoints, all preserved:
1. **Draft approval** (Phase 3) — before any shipment exists.
2. **Shipment→job approval** (Phase 5) — before any job exists.
3. **JobOrder Verify → Approve** (existing) — before any **bill / accounting /
   GST** exists (`GenerateBillingAsync` gate above).

The AI pipeline stops at creating a **Submitted** JobOrder. Billing, vouchers and
GST fire only when a human approves that job in the existing JobOrder screens —
exactly as for a manually keyed job.

## 2. Master business reference propagation (DHL Invoice Number)

Phase 5 writes the **DHL Invoice Number** into the job's mandatory
`CustomerInvoiceNumber` (`ShipmentJobApprovalService.ApproveAsync`). Because
existing code copies that field onto the `Bill` (`BillService.cs:97/257`) and the
invoice PDF prints it as the reference line, the single master reference threads
end-to-end:

```
IncomingEmail → ShipmentDraftApproval → (AWB/Export shipment)
             → ShipmentJobApproval → JobOrder.CustomerInvoiceNumber
             → Bill.CustomerInvoiceNumber → Voucher / GST / Customer Invoice
```

No duplicate reference is introduced; billing-group consolidation by
`CustomerInvoiceNumber` continues to work unchanged.

## 3. Manual end-to-end test (run in the app)

Prerequisite: an approver account; OpenAI key optional (heuristic fallback works).

1. **Seed an email** — let the inbox poll store a DHL email, or use an existing
   row in `IncomingEmails`.
2. `/ai-email-reader` — select the email, confirm the extracted draft, click
   **Create Draft Approval**.
3. `/email-approvals` — review, **Approve**. Expect: an AWB or Sea shipment is
   created (Phase 4) and a second approval is queued.
4. `/job-approvals` — pick **Clearing/Forwarding** + billing client, **Approve &
   Create Job**. Expect: a `JobOrder` (`CLR/FWD/FY/nnnn`), status **Submitted**,
   with `CustomerInvoiceNumber` = the DHL invoice number. **No bill yet.**
5. Open the JobOrder in the existing screens → **Verify** → **Approve**.
   Expect (all via existing logic):
   - a **Bill** appears for the job, `CustomerInvoiceNumber` matching;
   - **accounting vouchers** posted (A/R, Revenue, GST output);
   - the bill is eligible for the existing **Customer Invoice** issue flow;
   - the Phase-4 shipment is eligible for a **Transportation Bill** via the
     existing TB screen.

If steps 4–5 behave identically to a manually created job, Phase 6 is satisfied.

## 4. What was NOT done (by design)

- No new billing/accounting/voucher/GST/invoice code.
- No schema changes.
- No changes to approval gating or existing service signatures.

> Note: the live orchestrator billing run (step 5) must be exercised in the
> running app with an authenticated approver — it was **not** executed in the
> build-time verification, which covered the code path by inspection.
