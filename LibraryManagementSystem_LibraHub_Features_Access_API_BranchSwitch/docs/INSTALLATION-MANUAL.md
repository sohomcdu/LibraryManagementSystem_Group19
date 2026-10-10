# Installation and Running Manual

## Clean Windows VM prerequisites
1. Install Visual Studio 2022 with ASP.NET and web development tools and the .NET 10 SDK.
2. Install/enable SQL Server LocalDB (or configure a reachable SQL Server instance).
3. Copy the project ZIP to the VM and extract it to a writable folder.

## Start the project
1. Open `LibraryManagementSystem.slnx` in Visual Studio.
2. Open `appsettings.json` and verify `ConnectionStrings:DefaultConnection`. The default uses `(localdb)\mssqllocaldb` and database `LibraryDb`.
3. Restore NuGet packages.
4. Build the solution.
5. Run the project with the HTTPS launch profile. On startup the app applies EF Core migrations and seeds sample branches, desks, accounts, books, music, toys, and borrowers.
6. Open the displayed localhost URL and use the seeded demo accounts in the root README.

## Branch setup
The same codebase and install steps are used at each branch. Seeded branches are Central (`CEN`), Northside (`NTH`), and Riverside (`RIV`). Catalogue search can filter by branch. Staff can transfer an available item from its current branch to another; the item remains `InTransit` until staff confirms receipt.

## Troubleshooting
- If database startup fails, check LocalDB is installed/running and the connection string is valid.
- If a migration error occurs on a previously modified database, back up the database before applying manual repair; do not delete a database containing needed records.
- On a clean VM, capture the first successful migration/startup and verify login, search, kiosk, API, import, transfer, reservations, notifications, and exports before submission.
