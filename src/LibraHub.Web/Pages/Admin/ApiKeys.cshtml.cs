using LibraHub.Domain;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Admin;

public class ApiKeysModel : AppPageModel
{
    public List<ApiKey> Keys { get; private set; } = new();
    [BindProperty] public string NewName { get; set; } = "";
    public string? JustCreatedPlain { get; private set; }

    public async Task OnGetAsync() => Keys = await Db.ApiKeys.OrderByDescending(k => k.CreatedAt).ToListAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName)) { Err = "Name the key (e.g. the app or partner)."; await OnGetAsync(); return Page(); }
        var (key, plain) = await Svc<ApiKeyService>().CreateAsync(NewName);
        await OnGetAsync();
        JustCreatedPlain = plain;
        Msg = $"Key “{key.Name}” created — copy it now, it will not be shown again.";
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(int id)
    {
        await Svc<ApiKeyService>().RevokeAsync(id);
        Flash = "Key revoked.";
        return RedirectToPage();
    }
}
