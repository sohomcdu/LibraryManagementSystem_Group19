using System.ComponentModel.DataAnnotations;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Staff.Items;

public class AddModel : AppPageModel
{
    [BindProperty, Required, StringLength(200)] public string Title { get; set; } = "";
    [BindProperty, Required, StringLength(150)] public string Author { get; set; } = "";
    [BindProperty, Required] public string Isbn { get; set; } = "";
    [BindProperty] public string? Subtitle { get; set; }
    [BindProperty] public int CategoryId { get; set; }
    [BindProperty] public int BranchId { get; set; }
    [BindProperty] public int? Year { get; set; }
    [BindProperty] public string? Publisher { get; set; }
    [BindProperty, Range(1, 20)] public int Copies { get; set; } = 1;

    public List<SelectListItem> Categories { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Categories = (await Db.Categories.OrderBy(c => c.Name).ToListAsync())
            .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
        Branches = (await Db.Branches.OrderBy(b => b.Id).ToListAsync())
            .Select(b => new SelectListItem(b.Name, b.Id.ToString())).ToList();

        if (BranchRestriction.HasValue)
        {
            BranchId = BranchRestriction.Value;
        }
        else if (int.TryParse(Branches.FirstOrDefault()?.Value, out var first))
        {
            BranchId = first;
        }
        else
        {
            BranchId = 0;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await OnGetAsync();

        // Use the fully-qualified name to avoid the property-name shadow.
        // If the class lives in LibraHub.Domain, use:
        //     LibraHub.Domain.Isbn.IsValid(...)
        // If it lives in LibraHub.Infrastructure, use:
        //     LibraHub.Infrastructure.Isbn.IsValid(...)
        // (whichever the compiler accepts — try Domain first)

        if (!LibraHub.Infrastructure.Isbn.IsValid(this.Isbn))
        {
            Err = "Invalid ISBN (check digit failed).";
            return Page();
        }

        var isbn13 = LibraHub.Infrastructure.Isbn.ToIsbn13(this.Isbn);

        var branch = await Db.Branches.FindAsync(BranchId);
        var cat = await Db.Categories.FindAsync(CategoryId);
        if (branch == null || cat == null) { Err = "Choose a branch and category."; return Page(); }

        var codes = await Db.Items.Where(i => i.Code.StartsWith("BK-")).Select(i => i.Code).ToListAsync();
        var next = Math.Max(
            codes.Select(c => int.TryParse(c.AsSpan(3), out var n) ? n : 0).DefaultIfEmpty(100000).Max(),
            100000) + 1;

        for (int k = 0; k < Copies; k++)
        {
            var item = new Item
            {
                Code = $"BK-{next++:D6}",
                Title = Title.Trim(),
                Subtitle = Subtitle,
                Author = Author.Trim(),
                Isbn = isbn13,
                CategoryId = CategoryId,
                Publisher = Publisher,
                Year = Year,
                HomeBranchId = BranchId,
                CurrentBranchId = BranchId,
                CoverColors = ImportService.CoverFor(Title),
                Status = ItemStatus.Available,
                CreatedAt = Clock.Now
            };
            item.SearchText = ImportService.SearchTextFor(item, cat.Name);
            Db.Items.Add(item);
            await Svc<ItemLifecycleService>().OnItemAddedAsync(item, CurrentUserName);
        }

        await Db.SaveChangesAsync();
        Flash = $"{Copies} cop{(Copies == 1 ? "y" : "ies")} of \"{Title}\" added.";
        return RedirectToPage("/Staff/Items/Index");
    }
}
