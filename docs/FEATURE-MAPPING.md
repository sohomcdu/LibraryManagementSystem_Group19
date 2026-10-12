# Assignment Feature Mapping

| Requirement | Implementation in this MVC baseline | Demo route |
|---|---|---|
| Touch kiosk | Touch-first account lookup and item-code checkout; receipt is logged | `/Kiosk` |
| Public read-only REST API | Available items, categories, operational status; optional query/category/branch filters | `/api/public/items`, `/api/public/categories`, `/api/public/status` |
| Simulated notifications | Admin reviews borrow receipt, hold-ready and transfer-arrived records; can run the due-date sweep | `/Notification` |
| Multi-branch inventory | Admin manages branches, desks and item transfers | `/Branch`, `/Transfer` |
| Reservation/waitlist | Member places a hold for borrowed/damaged item; Admin manages the hold queue; FIFO promotion occurs when returned | `/Reservation`, `/Reservation/Holds` |
| CSV importer | Admin upload, validation, import-job history | `/Import` |
| Manager analytics and export | Borrowing, fines and inventory statistics; CSV and PDF exports (Manager) | `/Manager` |

## Known limitations to disclose
- Notification delivery is simulated in the database, not a real email/SMS gateway.
- Kiosk email lookup is for a supervised demo terminal only; a production kiosk needs a library card token/PIN and hardened kiosk session handling.
- The CSV parser expects simple comma-separated values and does not currently support commas escaped inside quoted fields.
- Branch assignment is seeded and managed via transfers; this is not a geographically distributed production deployment.
