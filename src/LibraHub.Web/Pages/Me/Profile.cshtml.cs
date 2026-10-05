using LibraHub.Domain;
using Microsoft.AspNetCore.Mvc;

namespace LibraHub.Pages.Me;

public class ProfileModel : AppPageModel
{
    public AppUser Patron { get; private set; } = null!;
    [BindProperty] public string? Mobile { get; set; }
    [BindProperty] public bool BorrowEmail { get; set; } public bool BorrowSms { get; set; }
    [BindProperty] public bool DueEmail { get; set; } public bool DueSms { get; set; }
    [BindProperty] public bool OverdueEmail { get; set; } public bool OverdueSms { get; set; }
    [BindProperty] public bool HoldEmail { get; set; } public bool HoldSms { get; set; }

    async Task LoadAsync()
    {
        Patron = await Db.Users.FindAsync(CurrentUserId) ?? throw new InvalidOperationException();
        Mobile = Patron.Mobile;
        BorrowEmail = Patron.PrefBorrow.HasFlag(Channels.Email); BorrowSms = Patron.PrefBorrow.HasFlag(Channels.Sms);
        DueEmail = Patron.PrefDueSoon.HasFlag(Channels.Email); DueSms = Patron.PrefDueSoon.HasFlag(Channels.Sms);
        OverdueEmail = Patron.PrefOverdue.HasFlag(Channels.Email); OverdueSms = Patron.PrefOverdue.HasFlag(Channels.Sms);
        HoldEmail = Patron.PrefHold.HasFlag(Channels.Email); HoldSms = Patron.PrefHold.HasFlag(Channels.Sms);
    }
    public async Task OnGetAsync() => await LoadAsync();

    static Channels C(bool e, bool s) => (e ? Channels.Email : 0) | (s ? Channels.Sms : 0);

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if ((BorrowSms || DueSms || OverdueSms || HoldSms) && string.IsNullOrWhiteSpace(Mobile))
        { FlashError = "Add a mobile number before enabling SMS."; return RedirectToPage(); }
        Patron.Mobile = Mobile;
        Patron.PrefBorrow = C(BorrowEmail, BorrowSms); Patron.PrefDueSoon = C(DueEmail, DueSms);
        Patron.PrefOverdue = C(OverdueEmail, OverdueSms); Patron.PrefHold = C(HoldEmail, HoldSms);
        await Db.SaveChangesAsync();
        Flash = "Preferences updated.";
        return RedirectToPage();
    }
}
