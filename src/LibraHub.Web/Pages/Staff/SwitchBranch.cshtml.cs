using LibraHub.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Staff;

/// <summary>Manager/Admin branch switcher used by the staff layout header.</summary>
public class SwitchBranchModel : AppPageModel
{
    // Accept nullable id and validate server-side so malformed/missing values don't produce a raw 400.
    public async Task<IActionResult> OnPostAsync(int? id, string? returnUrl)
    {
        if (!id.HasValue)
        {
            Err = "Invalid branch identifier.";
            return Page();
        }

        // Only allow switching for authorized roles
        if (!Svc<BranchContext>().CanSwitch)
        {
            Err = "You are not authorised to switch branches.";
            return Page();
        }

        // ensure branch exists before storing in session
        var exists = await Db.Branches.AsNoTracking().AnyAsync(b => b.Id == id.Value);
        if (!exists)
        {
            Err = "Selected branch does not exist.";
            return Page();
        }

        HttpContext.Session.SetInt32(BranchContext.SessionKey, id.Value);
        Msg = "Branch switched.";
        return LocalRedirect(string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/Staff" : returnUrl);
    }
}
