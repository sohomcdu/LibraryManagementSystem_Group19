# Pre-submission Test Checklist

Run these checks on a clean Windows VM and record actual results, date, tester, and evidence screenshots.

- [ ] Restore NuGet packages and build without errors.
- [ ] First-run database migrations and seed data complete successfully.
- [ ] Admin, Reception, and Manager demo logins work; access-denied checks work for restricted pages.
- [ ] Public search finds books, music, and toys; branch filter works.
- [ ] `/api/public/items`, `/api/public/categories`, and `/api/public/status` return JSON without authentication.
- [ ] Kiosk account lookup displays active loans; checkout of an available library code creates a loan and notification.
- [ ] Borrowing and return update item status and calculate fines as expected.
- [ ] A Member can place a waitlist hold for a borrowed/damaged item; duplicate active holds are prevented.
- [ ] Returning an item promotes the next waitlisted patron and logs a hold-ready notification.
- [ ] Transfer sets item `InTransit`; receiving transfer changes its branch and logs notification.
- [ ] Valid CSV imports; invalid CSV shows errors and does not silently save invalid rows.
- [ ] Due-date sweep logs due-soon/overdue notification records.
- [ ] Manager can download borrowing/fines/inventory CSV and PDF reports.
- [ ] Repeat startup does not duplicate seeded branches or the additional music/toy records.

Do not mark a check complete until it has actually been run.
