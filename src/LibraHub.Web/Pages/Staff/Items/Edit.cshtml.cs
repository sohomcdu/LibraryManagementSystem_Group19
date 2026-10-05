using System.ComponentModel.DataAnnotations;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Staff.Items;

/// <summary>Mockup 10 – Edit item + movement history. Retiring an item is blocked while it is on loan or reserved.</summary>
public class EditModel : AppPageModel
{
    public Item Item { get; private set; } = null!;
    public List<ItemEvent> History { get; private set; } = new();
    public List<SelectListItem> Categories { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();

    [BindProperty, Required] public string Title { get; set; } = "";
    [BindProperty, Required] public string Author { get; set; } = "";
    [BindProperty] public int CategoryId { get; set; }
    [BindProperty] public int? Year { get; set; }
    [BindProperty] public int HomeBranchId { get; set; }
    [BindProperty] public string Status { get; set; } = "";
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public int Version { get; set; }

    async Task LoadAsync(int id)
    {
        Item = await Db.Items.Include(i => i.Category).Include(i => i.CurrentBranch).Include(i => i.HomeBranch).FirstAsync(i => i.Id == id);
        History = await Db.ItemEvents.Where(e => e.ItemId == id).OrderByDescending(e => e.At).Take(10).ToListAsync();
        Categories = (await Db.Categories.OrderBy(c => c.Name).ToListAsync()).Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == Item.CategoryId)).ToList();
        Branches = (await Db.Branches.OrderBy(b => b.Id).ToListAsync()).Select(b => new SelectListItem(b.Name, b.Id.ToString(), b.Id == Item.HomeBranchId)).ToList();
        Title = Item.Title; Author = Item.Author; CategoryId = Item.CategoryId; Year = Item.Year; HomeBranchId = Item.HomeBranchId;
        Status = Item.Status.ToString(); Description = Item.Description; Version = Item.Version;
    }

    public async Task<IActionResult> OnGetAsync(int id) { await LoadAsync(id); return Page(); }

    public async Task<IActionResult> OnPostSaveAsync(int id)
    {
        await LoadAsync(id);
        var entity = await Db.Items.FirstAsync(i => i.Id == id);
        if (entity.Version != Version) { Err = "Someone else changed this item just now — reload and try again."; return Page(); }
        var wantStatus = Enum.Parse<ItemStatus>(Status);
        if (wantStatus != entity.Status && !(entity.Status == ItemStatus.Available && wantStatus == ItemStatus.Damaged))
        { Err = "Use the desk workflows (check-in/repair/transfer) to change status — direct status edits are limited to Available → Damaged."; return Page(); }

        entity.Title = Title.Trim(); entity.Author = Author.Trim(); entity.CategoryId = CategoryId; entity.Year = Year;
        entity.HomeBranchId = HomeBranchId; entity.Description = Description;
        if (wantStatus == ItemStatus.Damaged && entity.Status == ItemStatus.Available)
        { entity.Status = ItemStatus.Damaged; entity.StatusNote = $"In repair · est. back {Fmt.Day(Clock.Today.AddDays(14))}"; }
        entity.Version++;
        var cat = await Db.Categories.FindAsync(CategoryId);
        entity.SearchText = ImportService.SearchTextFor(entity, cat?.Name ?? "");
        Db.ItemEvents.Add(new ItemEvent { ItemId = id, Text = "Details edited", By = CurrentUserName, At = Clock.Now });
        await Db.SaveChangesAsync();
        Flash = "Item updated.";
        return RedirectToPage("/Staff/Items/Edit", new { id });
    }

    public async Task<IActionResult> OnPostRetireAsync(int id)
    {
        var item = await Db.Items.FindAsync(id);
        if (item == null) return RedirectToPage("/Staff/Items/Index");
        if (item.Status is ItemStatus.Borrowed or ItemStatus.Reserved or ItemStatus.InTransit)
        { FlashError = "Cannot retire while borrowed, reserved or in transit."; return RedirectToPage(new { id }); }
        Db.ItemEvents.Add(new ItemEvent { ItemId = id, Text = "Retired from catalogue", By = CurrentUserName, At = Clock.Now });
        Db.Items.Remove(item);
        await Db.SaveChangesAsync();
        Flash = "Item retired.";
        return RedirectToPage("/Staff/Items/Index");
    }
}
