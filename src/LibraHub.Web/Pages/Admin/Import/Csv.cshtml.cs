using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Admin.Import;

/// <summary>Mockup 14 – CSV upload, validate, commit.</summary>
public class CsvModel : AppPageModel
{
    const string PreviewKey = "import.preview";
    [BindProperty] public new IFormFile? File { get; set; }
    [BindProperty] public int DefaultBranchId { get; set; }
    [BindProperty] public string Policy { get; set; } = "Skip";
    public List<SelectListItem> Branches { get; private set; } = new();
    public ImportPreview? Preview { get; private set; }
    public string? FileName { get; private set; }

    public async Task OnGetAsync()
    {
        Branches = (await Db.Branches.OrderBy(b => b.Id).ToListAsync()).Select(b => new SelectListItem(b.Name, b.Id.ToString())).ToList();
        DefaultBranchId = Branches.FirstOrDefault()?.Value is string s ? int.Parse(s) : 0;
    }

    public async Task<IActionResult> OnPostValidateAsync()
    {
        await OnGetAsync();
        if (File == null || File.Length == 0) { Err = "Choose a CSV file first."; return Page(); }
        if (File.Length > ImportService.MaxBytes) { Err = "File is larger than 5 MB."; return Page(); }
        using var reader = new StreamReader(File.OpenReadStream());
        var text = await reader.ReadToEndAsync();
        var pv = await Svc<ImportService>().ValidateAsync(text, DefaultBranchId, Enum.Parse<DuplicatePolicy>(Policy));
        if (pv.FileError != null) { Err = pv.FileError; return Page(); }
        HttpContext.Session.SetObj(PreviewKey, pv);
        HttpContext.Session.SetString(PreviewKey + ".file", File.FileName);
        Preview = pv; FileName = File.FileName;
        return Page();
    }

    public async Task<IActionResult> OnPostCommitAsync()
    {
        await OnGetAsync();
        var pv = HttpContext.Session.GetObj<ImportPreview>(PreviewKey);
        if (pv == null) { Err = "Validate a file first."; return Page(); }
        var job = await Svc<ImportService>().CommitAsync(pv, "CSV · " + (HttpContext.Session.GetString(PreviewKey + ".file") ?? "upload.csv"), CurrentUserName);
        HttpContext.Session.Remove(PreviewKey);
        Flash = $"Imported {job.Imported} row(s), skipped {job.Skipped}.";
        return RedirectToPage();
    }

    public IActionResult OnGetTemplate() =>
        File_(ImportService.Template, "librahub-import-template.csv");

    public IActionResult OnGetErrors()
    {
        var pv = HttpContext.Session.GetObj<ImportPreview>(PreviewKey);
        if (pv == null) return NotFound();
        var sb = new System.Text.StringBuilder("Row,Result,Messages\r\n");
        foreach (var r in pv.Rows.Where(r => r.State != RowState.Ok))
            sb.AppendLine(CsvUtil.Line(r.Line, r.State.ToString(), string.Join(" | ", r.Messages)));
        return File_(sb.ToString(), "import-errors.csv");
    }

    FileContentResult File_(string csv, string name) =>
        new(new System.Text.UTF8Encoding(true).GetBytes(csv), "text/csv") { FileDownloadName = name };
}
