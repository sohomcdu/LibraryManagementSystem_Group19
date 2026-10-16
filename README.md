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
- Read-only REST API: `/api/public/*` (open) and `/api/v1/*` (needs `X-API-Key`). Documentation at `/ApiDocs`.
- API keys: `/ApiKeys` (Admin). API usage report: `/ApiUsage` (Manager and Admin).

### REST API
The same five read-only endpoints are served on two routes that return identical JSON:

| Route | For | Key |
|---|---|---|
| `/api/public/...` | Open discovery, e.g. a website widget | Not needed |
| `/api/v1/...` | Registered integrations | `X-API-Key` header, issued by an Admin |

| Endpoint | Returns |
|---|---|
| `GET /items` | Catalogue search, paged. Options: `q`, `type` (book/music/toy), `category`, `branch` (CEN/NTH/RIV), `status` (available/all), `page`, `pageSize` (max 100) |
| `GET /items/{id}` | One item in full (ISBN, release year or minimum age), or 404 |
| `GET /categories` | Item types and their categories with available counts |
| `GET /branches` | Branches, open/closed, and available items per branch |
| `GET /status` | Service status and a catalogue summary |

Examples: `/api/public/items?q=code`, `/api/public/items?type=toy&branch=NTH`, `/api/public/items/1`, `/api/public/branches`.

Errors use `application/problem+json`: 400 for an invalid option, 401 for a missing, invalid or revoked key on `/api/v1`, 404 for an unknown item, 405 for anything other than GET. The API never exposes borrower information and cannot change data.

Every API call is logged (`ApiRequestLogs` table). Admins see each key's last-used time and request count at `/ApiKeys`; Managers see request volume, integrations, rejected calls, top endpoints and top searches at `/ApiUsage`, with CSV export.

## CSV import format
**Sample catalogue on first run:** when the app starts it automatically loads `docs/sample-data/books.csv`, `music.csv` and `toys.csv` (50 books, 50 albums and 50 toys). Items already in the database are skipped, so restarting never creates duplicates. Sources are listed in.
Admin → **CSV Importer** (`/Import`) bulk-adds books, music or toys. The importer recognises the file type from the header row and reads columns by name, so their order does not matter. Templates can be downloaded from the importer page, and ready-made sample files are in `docs/sample-data/`

| Type | Header row |
|---|---|
| Books | `Title,Author,ISBN,Category,BranchCode,Year,Copies,LibraryCode,Description` |
| Music | `Title,Artist,ReleaseYear,BranchCode,Copies,LibraryCode,Description` |
| Toys | `Title,Type,MinimumAge,BranchCode,Copies,LibraryCode,Description` |

Rules:
- Title and BranchCode (`CEN`, `NTH` or `RIV`) are required on every row, plus Author and a 10 or 13 digit ISBN for books, Artist for music, and Type for toys.
- Toy Type must be one of Board Game, Puzzle, Building Blocks, Action Figure, Plush, Educational or Outdoor.
- LibraryCode is optional. When it is blank the next free code is generated (`BK-1xxx` books, `MU-2xxx` music, `TY-3xxx` toys). A given code must be unique and needs `Copies` = 1, because every copy has its own code.
- Values containing commas must be wrapped in double quotes.
- The whole file is validated first. If any row has an error, nothing is saved and the errors are listed.
- If an item is already in the catalogue (books match on ISBN, music on title and artist, toys on title), you can choose to skip it, add it again as extra copies, or stop the import.

```csv
Title,Author,ISBN,Category,BranchCode,Year,Copies,LibraryCode,Description
Example Book,Jordan Lee,9780306406157,Fiction,CEN,2024,2,,A short description of the book.
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

### REST API

See the **REST API** section above, or open `/ApiDocs` in the running app.


## LibraHub-style workflows added in this MVC version

- **Branch context:** Reception and Manager can switch the active branch from the top navigation. The selection is stored in a secure, HTTP-only cookie; Reception request views and Manager reports use it to scope the view. Admin circulation tools operate across all branches. Clear the `LMS.BranchCode` cookie to return to the default branch.
- **Kiosk:** activate a shared device once with a seeded kiosk account. The kiosk account is limited by controller role checks; the application no longer traps the kiosk account on `/Kiosk`, so public Home and Catalogue links work. The device is assigned to its home branch and cannot check out another branch's item.
- **REST API:** `/api/public/*` (open) and `/api/v1/*` (requires `X-API-Key`) serve the same read-only endpoints: items, items/{id}, categories, branches, status. Key checking and request logging live in `Services/ApiAccessFilter.cs`. Admins manage keys at `/ApiKeys`; raw keys are displayed once and only SHA-256 hashes are stored.

### Test API key flow
1. Sign in as `admin@library.local` (demo password `Admin@12345`).
2. Open Administration → REST API keys and generate a key for a named client. Copy the key when shown.
3. Use the **Try the API** panel on the same page (paste the key, pick an endpoint, Send), or call it from PowerShell: `Invoke-RestMethod "https://localhost:7202/api/v1/items" -Headers @{ "X-API-Key" = "<copied-key>" }`. Missing, invalid or revoked keys return HTTP 401.
4. Revoke the key in the admin screen and confirm it no longer works.
5. Sign in as `manager@library.local` (demo password `Manager@12345`) and open **API usage** to see the calls you just made.

The application applies migrations at startup, including `AddApiKeys` and `AddApiUsageTracking`. Review demo credentials and rotate them before any deployment beyond local coursework use.
