# Library Management System — Group Project Edition

An ASP.NET Core MVC / .NET 10 library system extended from the team's Assignment 2 baseline. The project keeps the existing Books, Music, Toys, borrowing, member requests, and manager dashboard while adding multi-branch inventory, transfers, reservations/waitlist, simulated notifications, CSV importing, exports, a touch-first kiosk, and a read-only public API. The kiosk UI and workflow take inspiration from the team's latest LibraHub prototype while remaining MVC views/controllers.

## Requirements
- Windows 10/11
- Visual Studio 2022 with the .NET 10 SDK/workload
- SQL Server LocalDB (installed with Visual Studio) or a SQL Server instance
- Internet access on first restore if NuGet packages are not cached

## Run locally
1. Extract the ZIP and open `LibraryManagementSystem.slnx` in Visual Studio.
2. Confirm `appsettings.json` connection string `DefaultConnection` points to your SQL Server/LocalDB. Default: `(localdb)\mssqllocaldb`, database `LibraryDb`.
3. Restore NuGet packages, then build the solution.
4. Run using the HTTPS launch profile. At startup, the application applies EF Core migrations and seeds demo data idempotently.
5. If LocalDB is unavailable, change `DefaultConnection` to a reachable SQL Server connection string. Use the same setup at Central, Northside, and Riverside; branch inventory is selected in seed data and the transfer module rather than by changing source code.

> First-run note: startup migrations need permission to create/update the `LibraryDb` database. For a clean VM, install SQL Server LocalDB first. Verify the first run on a fresh Windows VM before submitting, as required by the brief.

## Demo accounts
All seeded staff passwords are for local assessment/demo only; change them before any deployment.

| Role | Email | Password |
|---|---|---|
| Admin | `admin@library.local` | `Admin@12345` |
| Reception | `reception@library.local` | `Reception@12345` |
| Manager | `manager@library.local` | `Manager@12345` |
| Member | `member@library.local` | `Member@12345` |

The Development sign-in page provides quick-fill buttons for these role accounts. The kiosk activation page lists the seeded devices (`kiosk.central@librahub.local`, `kiosk.northside@librahub.local`, and `kiosk.riverside@librahub.local`); each uses `Password123!`. These credential panels are hidden outside Development.

Members can register from the Register page. Seeded borrowers include Alice Smith, Bob Johnson, Charlie Brown, Diana Prince, and Evan Wright.

## Main modules and routes
- Public branch directory: `/Branch` (branch management actions remain Admin-only).
- Member self-service dashboard: `/Me` (own active loans, overdue summary, active holds and notification history).
- Role-focused workspaces: Reception uses `/Staff` to view borrower records and branch requests only; Admin uses `/Staff` to manage borrowers, requests, circulation, the collection, branches, imports and API keys; Manager uses `/Manager` for branch-scoped reports and exports.
- Public patron catalogue/search: `/Search`
- Self-service kiosk: `/Kiosk` — account lookup by registered borrower email and checkout by library code; designed for a supervised physical terminal.
- Multi-branch management: `/Branch` (Admin)
- Item transfers: `/Transfer` (Admin)
- Reservation and waitlist queue: `/Reservation` (Member) and `/Reservation/Holds` (Admin)
- Simulated notifications dashboard: `/Notification` (Admin)
- CSV importer: `/Import` (Admin)
- Manager analytics and CSV/PDF export: `/Manager` (Manager)
- Public read-only API: `GET /api/public/items`, `GET /api/public/categories`, `GET /api/public/status`

### Public API examples
- `/api/public/items`
- `/api/public/items?q=chess`
- `/api/public/items?category=toy&branch=NTH`
- `/api/public/categories`
- `/api/public/status`

The public API exposes available catalogue records only and supports optional search/category/branch filters. It does not expose borrower information or staff-only operations.

## CSV import format
The importer expects a header row followed by columns in this order:

`Title,Author,ISBN,Category,BranchCode,Year,Copies,LibraryCode`

Use branch codes `CEN`, `NTH`, or `RIV`. ISBN must be 10 or 13 digits. Keep values comma-free unless the importer is upgraded to a quoted CSV parser. Example:

```csv
Title,Author,ISBN,Category,BranchCode,Year,Copies,LibraryCode
Example Book,Jordan Lee,9781234567890,Technology,CEN,2024,2,
```

## Seeded catalogue additions
- Music: Blue Train, Back to Black, Discovery, A Love Supreme, The Planets.
- Toys: World Map Jigsaw, Cooperative Board Game, Beginner Science Kit, Wooden Train Set, Tangram Shape Puzzle, Memory Matching Cards.
- Existing book/music/toy seed entries are retained. Seed logic checks stable library codes so restarting does not duplicate the additional entries.

## Recommended demonstration sequence
1. Open public search and search for a book, music item, and toy.
2. Open `/api/public/status` and `/api/public/items` to demonstrate public read-only integration.
3. Log in as Admin; use Loans and returns to check out an available item and confirm a simulated receipt appears in Notifications.
4. On a supervised kiosk terminal, sign in with the kiosk-device account, look up a borrower email and check out an available item using its library code.
5. Place a hold on an unavailable item, return it, and show queue promotion/notification in Holds and Notifications.
6. As Admin, start a transfer from one branch to another, then receive it and confirm branch inventory updates.
7. Log in as Admin to import CSV data and review Import Jobs.
8. Log in as Manager to view analytics and download CSV/PDF exports.

## Security and implementation notes
- MVC controller authorization attributes protect staff CRUD modules; Reception has read-only borrower/request access, while Admin manages those records and staff circulation; the public API is read-only and anonymous.
- The kiosk is designed for a supervised, physically controlled demo station. Its email-based lookup is a demonstration workflow, not strong identity verification for public deployment; production deployment should require a library card token/PIN or a kiosk-specific session mechanism.
- Notifications are simulated by database rows; no external email/SMS is sent.
- Please test build, migrations, role access, all demo paths, and installation on a clean VM before submission.


## LibraHub-inspired UI and access boundaries

This MVC edition keeps the Assignment 2 MVC codebase while adopting the LibraHub visual language: navy/teal hero sections, amber collection accents, icon-led collection cards, responsive catalogue filters, admin-only branch management, member self-service dashboard, role-focused Reception/Admin workspaces, manager reports, compact dropdown navigation, and large touch controls for kiosk use. The global navigation only displays links relevant to the signed-in role; controller `[Authorize]` attributes remain the real security boundary.

### Kiosk device access

Admin can open **Kiosk activation** from the Circulation menu to reach `/Kiosk/Activate`. A kiosk-device Identity account is required to enter `/Kiosk`; ordinary Member, Reception, or Admin accounts are not granted the `Kiosk` role. The kiosk's **Done · Sign out** button clears the device login. Demo kiosk accounts use `Password123!`:

- `kiosk.central@librahub.local`
- `kiosk.northside@librahub.local`
- `kiosk.riverside@librahub.local`

For production, replace shared demo credentials with per-device secrets and stronger patron card/PIN verification. The demonstration patron lookup still uses registered email and is not intended as production-grade identity verification.

### Public read-only API

- `GET /api/public/items` (optional `q` and `category` filters)
- `GET /api/public/categories`
- `GET /api/public/status`

These endpoints return read-only JSON and do not expose create/update/delete operations.


## LibraHub-style workflows added in this MVC version

- **Branch context:** Reception and Manager can switch the active branch from the top navigation. The selection is stored in a secure, HTTP-only cookie; Reception request views and Manager reports use it to scope the view. Admin circulation tools operate across all branches. Clear the `LMS.BranchCode` cookie to return to the default branch.
- **Kiosk:** activate a shared device once with a seeded kiosk account. The kiosk account is limited by controller role checks; the application no longer traps the kiosk account on `/Kiosk`, so public Home and Catalogue links work. The device is assigned to its home branch and cannot check out another branch's item.
- **REST API:** anonymous read-only discovery remains at `/api/public/items`, `/api/public/categories`, `/api/public/status`. Versioned integration routes `/api/v1/items`, `/api/v1/categories`, `/api/v1/status` require `X-API-Key`. Admins manage keys at `/ApiKeys`; raw keys are displayed once and only SHA-256 hashes are stored.

### Test API key flow
1. Sign in as `admin@library.local` (demo password `Admin@12345`).
2. Open Administration → REST API keys and generate a key for a named client. Copy the key when shown.
3. Call `GET /api/v1/items` with header `X-API-Key: <copied-key>`. Missing, invalid or revoked keys return HTTP 401.
4. Revoke the key in the admin screen and confirm it no longer works.

The application applies migrations at startup, including `AddApiKeys`. Review demo credentials and rotate them before any deployment beyond local coursework use.
