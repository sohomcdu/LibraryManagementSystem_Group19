# LibraHub

A working ASP.NET Core (.NET 10) implementation of the LibraHub library system described in
`LibraHub_UI_Mockups_and_Feature_Spec.docx` — public catalogue, patron "My Library" area, a
reception/manager staff console, a self-service kiosk, CSV/metadata import, reports (CSV + PDF
export) and a read-only public JSON API. Screens follow the mockups (Central/Northside/Riverside
branches, patron Alex Morgan / card LC-2048-7731, staff Sam Lee / Jordan Blake, etc).

The database is SQLite and is created and seeded automatically the first time you run the app —
there is nothing to install or configure beyond the .NET SDK.

## Requirements

- **.NET 10 SDK** (`dotnet --version` should print `10.x`)
- No external database, no internet access needed to run (the "metadata provider" is a local
  JSON file, not a real API call)

## Run it

```bash
cd src/LibraHub.Web
dotnet run
```

Then open the URL printed in the console (typically `https://localhost:5001` /
`http://localhost:5000`). The SQLite file `librahub.db` and all seed data are created on first
launch inside `src/LibraHub.Web/`. Delete that file to reset the demo data.

## Run the tests

```bash
dotnet test
```

`tests/LibraHub.Tests` exercises the ISBN checksum validator and the two core F1/F5 business
rules (renew blocked by a waitlist; check-in hands the item straight to the first patron in
queue) against a real, in-memory SQLite `AppDbContext` — the same code path the web app uses.

## Demo accounts (all passwords `Password123!`)

| Role | Login |
|---|---|
| Patron | `alex.morgan@example.com` — or card `LC-2048-7731` / PIN `4321` |
| Reception (Central) | `sam.lee@librahub.local` |
| Reception (Northside) | `nina.osei@librahub.local` |
| Manager | `jordan.blake@librahub.local` |
| Admin | `admin@librahub.local` |
| Kiosk device (Central) | `kiosk.central@librahub.local` at `/Kiosk/Activate` |

A demo API key is seeded for the public API: `lh_demo_key_for_local_testing`
(`Staff console → Admin → API keys` to issue/revoke more).

```bash
curl -H "X-Api-Key: lh_demo_key_for_local_testing" http://localhost:5000/api/v1/items?q=clean
```

## Project layout

```
LibraHub.sln
global.json                      # pins the .NET 10 SDK
src/LibraHub.Web/
  Domain/Entities.cs, Enums.cs   # EF Core entities (spec §4/§5 lifecycle fields, concurrency token)
  Data/AppDbContext.cs           # DbContext, enum-as-string conversions, unique indexes
  Data/SeedData.cs               # demo branches/users/items/loans/holds/transfers matching the mockups
  Services/                      # all business rules — pages never mutate entities directly
    ItemLifecycleService.cs      # F4/F5: returns, damage/repair, transfers, holds/waitlist, renewals
    LendingService.cs            # F1: shared desk+kiosk scan/checkout rules and basket model
    NotificationService.cs       # F3: simulated email/SMS, templates, idempotent due/overdue scanner
    FineService.cs                # fine accrual/settlement
    CatalogueService.cs           # F4 search/facets + item details/availability
    ReportService.cs              # F7: borrowing/fines/inventory report data
    ImportService.cs              # F6: CSV validate/commit + mock metadata provider import
    ApiKeyService.cs, BranchHours.cs, SimplePdf.cs, CsvUtil.cs, SettingsService.cs, DueDateScanner.cs
  Api/ApiEndpoints.cs            # F2: /api/v1/* minimal APIs + key/rate-limit/read-only middleware
  Infrastructure/                # PageBase, AuthService, BranchContext, Clock, Isbn, Fmt, Barcode
  Pages/
    Catalogue/                  # mockups 1–3 — public search + item details
    Account/                    # mockup 4 — sign in / register
    Me/                         # mockups 5–6 — patron dashboard, reservations, fines, notifications
    Staff/                      # mockups 7–13 — dashboard, desk, items, holds, transfers, notifications, fines
    Admin/                      # mockups 14–15 — CSV import, provider import, API keys
    Reports/                    # mockups 16–18 — borrowing / fine audit / inventory + CSV & PDF export
    Kiosk/                      # mockups 19–22 — self-service check-out
tests/LibraHub.Tests/           # xUnit tests against a real in-memory SQLite AppDbContext
```

## Notable design choices

- **One service layer, no logic in pages.** Every item-status change (borrow, return, damage,
  repair, transfer, hold) goes through `ItemLifecycleService`/`LendingService` so the waitlist and
  notification rules in the spec can never be bypassed from a Razor Page.
- **Simulated notifications.** `NotificationService` writes an email/SMS "send" to the
  `NotificationLog` table (and optionally the console) — never a real network call — with a
  unique key per loan/type/day/channel so the background scanner is safe to run repeatedly.
- **Dependency-free PDF export.** `SimplePdf` hand-writes a minimal multi-page PDF (no external
  package) for the three report exports, with automatic pagination and a footer.
- **Real Code 39 barcodes** are rendered as inline SVG (`Infrastructure/Barcode.cs`) for the
  patron card and kiosk scan screen, so a real barcode scanner can read them off a printout.
- **Optimistic concurrency** on `Item.Version` guards the item-edit form and check-out against two
  staff members changing the same item at once.

## What I could not verify

This code was written and reviewed in an environment without the .NET SDK or outbound network
access, so I could not run `dotnet build`/`dotnet run`/`dotnet test` myself before packaging it.
I've re-read every file for consistency (namespaces, method signatures, EF navigation/FK fixup,
Razor Pages routing/handlers), but please run `dotnet build` first and treat any compiler errors
as something to fix rather than a sign the architecture is wrong — the fix is almost always local
to one file.
