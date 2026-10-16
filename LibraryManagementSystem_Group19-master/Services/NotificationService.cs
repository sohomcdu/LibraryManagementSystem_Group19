using LibraryManagementSystem.Controllers;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Services
{
    /// <summary>
    /// Builds and logs simulated notifications using the message templates
    /// from the LibraHub spec appendix. "Sending" = writing a Notification
    /// row; nothing leaves the app.
    /// </summary>
    public  class NotificationService
    {
        public static async Task LogBorrowReceiptAsync(LibraryDbContext context, BorrowRecord record, Item item)
        {
            var borrower = await context.Borrowers.FindAsync(record.BorrowerId);
            if (borrower == null) return;

            var body = $"Hi {borrower.FullName}, you borrowed {item.Name} today. " +
                       $"It is due on {record.DueDate:yyyy-MM-dd}. Renew online unless someone is waiting.";

            context.Notifications.Add(new Notification
            {
                Type = "Borrow",
                Channel = "Email",
                Recipient = borrower.Email,
                Subject = $"Your LibraHub receipt — due {record.DueDate:yyyy-MM-dd}",
                Body = body
            });
            await context.SaveChangesAsync();
        }

        public static async Task LogHoldAvailableAsync(LibraryDbContext context, Reservation reservation)
        {
            var borrower = await context.Borrowers.FindAsync(reservation.BorrowerId);
            var item = await context.Items.FindAsync(reservation.ItemId);
            if (borrower == null || item == null) return;

            var body = $"Hi {borrower.FullName}, {item.Name} is ready for you. " +
                       $"Collect by {reservation.PickupExpiresAt:yyyy-MM-dd}.";

            context.Notifications.Add(new Notification
            {
                Type = "HoldAvailable",
                Channel = "Email",
                Recipient = borrower.Email,
                Subject = "Your hold is ready",
                Body = body
            });
            await context.SaveChangesAsync();
        }

        public static async Task LogTransferArrivedAsync(LibraryDbContext context, ItemTransfer transfer)
        {
            var item = await context.Items.FindAsync(transfer.ItemId);
            var toBranch = await context.Branches.FindAsync(transfer.ToBranchId);
            var fromBranch = await context.Branches.FindAsync(transfer.FromBranchId);
            if (item == null) return;

            context.Notifications.Add(new Notification
            {
                Type = "TransferArrived",
                Channel = "Staff",
                Recipient = toBranch?.Name ?? "Unknown branch",
                Subject = "Transfer received",
                Body = $"{item.Name} arrived at {toBranch?.Name} from {fromBranch?.Name}."
            });
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Called from a scheduled/manual sweep (e.g. a button on the
        /// Notification dashboard, or a background job if your team adds
        /// one) to log due-soon and overdue reminders for active loans.
        /// </summary>
        public static async Task RunDueDateSweepAsync(LibraryDbContext context)
        {
            var active = context.BorrowRecords.Where(r => r.ReturnDate == null).ToList();
            foreach (var record in active)
            {
                var borrower = await context.Borrowers.FindAsync(record.BorrowerId);
                var item = await context.Items.FindAsync(record.ItemId);
                if (borrower == null || item == null) continue;

                var daysUntilDue = (record.DueDate.Date - DateTime.Now.Date).Days;

                if (daysUntilDue == 1)
                {
                    context.Notifications.Add(new Notification
                    {
                        Type = "DueSoon",
                        Channel = "Email",
                        Recipient = borrower.Email,
                        Subject = $"{item.Name} is due tomorrow",
                        Body = $"Hi {borrower.FullName}, {item.Name} is due tomorrow. Renew or return it to avoid fines."
                    });
                }
                else if (daysUntilDue < 0)
                {
                    var daysOverdue = -daysUntilDue;
                    context.Notifications.Add(new Notification
                    {
                        Type = "Overdue",
                        Channel = "Email",
                        Recipient = borrower.Email,
                        Subject = $"{item.Name} is overdue",
                        Body = $"Hi {borrower.FullName}, {item.Name} is {daysOverdue} days overdue. " +
                               $"Current fine: {daysOverdue * BorrowRecordController.DailyFineRate:C}."
                    });
                }
            }
            await context.SaveChangesAsync();
        }
    }
}