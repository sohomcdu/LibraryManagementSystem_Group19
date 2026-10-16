# Patron User Guide

## Search the catalogue
1. Open **Search Catalogue** from the main navigation.
2. Enter a title, description term, or library code (for example `Clean Code`, `chess`, or `TY-3006`).
3. Optionally select Central, Northside, or Riverside to narrow results.
4. Check the item type, availability status, branch, and library code.
5. Select an item card to open its full details, including its cover image when available, complete description, and item-specific information. Members can request available items or join the waitlist for borrowed/damaged items from this page.
6. Sign in as a Member to request an available item or use the reservation/waitlist workflow for a borrowed/damaged item.

## Self checkout as a signed-in Member
1. Sign in to your Member account and open **Self checkout** from the navigation menu (or `/Kiosk`).
2. Your signed-in account is used automatically; you do not need to enter your email again.
3. Scan/type an available item's library code and select **Confirm checkout**. The item is checked out to your own linked borrower profile and a simulated receipt is recorded.
4. Select **Done · Back to my library** when finished. This returns to self checkout without signing you out of your Member account.

## Use the self-service kiosk
1. Open `/Kiosk` on the supervised kiosk station.
2. Enter the email registered to the library borrower profile and select **View my account** to see active loans and due dates.
3. To check out an item, enter the registered email and scan/type the item's library code, then select **Confirm checkout**.
4. Read the result message and due date. The simulated receipt appears in the Notifications dashboard for staff. Kiosk-device checkout is restricted to items at that device's branch.
5. Ask Reception for help if the email or item code is not recognised.

## Reservations
Members can join a waitlist when an item is borrowed or damaged. Queue order is based on request time. When an item is returned, the next waiting reservation becomes ready for pickup and a simulated notification is recorded. Admin can fulfil ready holds from the Holds page.

## Branch transfer requests (staff)
1. Sign in as Admin and open **Item transfers** in the Circulation menu.
2. Filter by Books, Music, or Toys and the source branch, then search the available inventory by item ID, library code, or name. Select the exact copies; the selected count is the requested quantity. Sending a request does not remove stock.
3. Sign in as Manager and open **Transfer requests**. Review the selected item IDs and codes and check which copies remain available at the source.
4. Approve a request only when enough stock is available, or reject it. Approval moves the selected available items into transit; Admin can mark each item received from the Item transfers page.

## Reception desk
Reception staff can check items out to borrowers and check items in from **Staff workspace** or the **Operations** navigation menu. Available stock and loans are limited to the currently selected branch. The Reception workspace also links to **Kiosk service** to activate that branch's registered self-service device; after activation, use the kiosk's **End kiosk session** action to sign out of kiosk mode.
For checkout, select Books, Music, or Toys to load available items at the current branch, then filter by library code or item name and choose the copy to issue. Type the beginning of a borrower's name to see matching names (for example, `A` or `AC`) and select the correct borrower from the suggestions before confirming checkout.

## API examples
- `/api/public/items` lists available catalogue items.
- `/api/public/items?q=chess` searches available items.
- `/api/public/items?category=toy&branch=NTH` filters by type and branch.
- `/api/public/categories` lists supported catalogue categories.
- `/api/public/status` returns operating status and active branch details.
