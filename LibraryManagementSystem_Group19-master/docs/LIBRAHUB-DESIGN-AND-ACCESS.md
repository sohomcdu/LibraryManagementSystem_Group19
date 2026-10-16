# LibraHub-inspired design and access boundaries

## UI system
- Navy header and brand mark, teal primary actions, amber accent, slate helper text, rounded cards, and responsive touch-sized kiosk controls.
- Icon-led role-based navigation; links are hidden when the role should not see that module.
- Public catalogue search stays available to signed-out users.

## Access boundaries
- Admin: borrower and request management, catalogue administration, branches, imports, circulation, transfers, holds, and notification tools.
- Reception: read-only borrower records and branch-scoped borrow requests.
- Manager: branch-scoped reports and exports.
- Member: own requests and reservations; may use self checkout while signed in. Checkout uses the Member's linked borrower profile rather than a submitted email.
- Kiosk: kiosk routes only; requests to other application pages redirect to `/Kiosk`. Device accounts remain restricted to their home branch.
- Public API: anonymous GET-only catalogue/categories/status endpoints. No mutation endpoints are exposed.

## Kiosk device flow
1. Admin opens Kiosk activation from the Circulation menu.
2. `/Kiosk/Activate` accepts a registered kiosk device account.
3. A signed-in `Member` can also open `/Kiosk`, where their own account is connected automatically. Kiosk-device accounts continue to use the supervised email lookup.
4. Kiosk-device checkout is restricted to the device's assigned home branch; Member checkout is attributed to the authenticated Member.
5. The Done button signs out a kiosk device and returns to activation. For a Member, it returns to self checkout without ending the Member session.

## Verification checklist
- [ ] Signed-out user can search the public catalogue.
- [ ] Member cannot open admin CRUD, transfers, or staff notification pages.
- [ ] Reception can view borrower records and branch requests only; Admin has the management, circulation, and kiosk-activation actions.
- [ ] A normal Member/Reception login is rejected as a kiosk device.
- [ ] Kiosk role is redirected away from `/Home`, `/Manager`, `/Import`, and `/api/public/status`.
- [ ] Public API endpoints return JSON for GET requests and expose no write operations.
- [ ] Done signs out the kiosk account.
- [ ] A signed-in Member can self-checkout an available item using their own linked borrower profile without entering another patron's email.
- [ ] Public item cards open a full details page with the applicable cover and item-specific metadata; Members can request/hold the item from there.
- [ ] Test all flows after migrations on a clean Windows VM.

> Build limitation: this package was checked statically in the current environment; run `dotnet restore` and `dotnet build` with the .NET 10 SDK before treating it as verified.


## Branch context and REST API authentication
- Reception and Manager can select the active branch in the header. The selection is stored in an HTTP-only cookie and scopes Reception's pending requests and Manager reports. Admin circulation tools operate across all branches. The seeded default is CEN (Central Library).
- Kiosk device activation assigns the terminal to its home branch (CEN/NTH/RIV). Checkout rejects an item assigned to another branch. The kiosk role is not globally redirected away from public catalogue/home links.
- `/api/public/items`, `/api/public/categories`, `/api/public/status` are anonymous, read-only discovery endpoints. `/api/v1/items`, `/api/v1/categories`, `/api/v1/status` are versioned read-only endpoints requiring `X-API-Key`; API keys are generated and revoked by Admin, stored only as SHA-256 hashes, and displayed once on creation.
