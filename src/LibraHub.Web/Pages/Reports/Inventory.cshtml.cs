using LibraHub.Domain;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Reports;

/// <summary>Mockup 18 – Inventory health.</summary>
public class InventoryModel : ReportPageBase
{
    public InventoryReport R { get; private set; } = null!;
    public async Task OnGetAsync(string? from, string? to, int? branch, int? category)
    {
        await BindFilterAsync(from, to, branch, category);
        R = await Svc<ReportService>().InventoryAsync(Filter);
    }

    public async Task<IActionResult> OnGetCsvAsync(string? from, string? to, int? branch, int? category)
    {
        await BindFilterAsync(from, to, branch, category);
        var rep = await Svc<ReportService>().InventoryAsync(Filter);
        return new CsvStreamResult("inventory-health.csv", async w =>
        {
            await w.WriteAsync(CsvUtil.Line("Branch", "Items", "Damaged", "Overdue", "IdleLast12M", "DamagedPct", "IdlePct", "Health") + "\r\n");
            foreach (var b in rep.Branches)
                await w.WriteAsync(CsvUtil.Line(b.Branch, b.Items, b.Damaged, b.Overdue, b.Idle, b.DamagedPct, b.IdlePct, b.Good ? "Good" : "Watch") + "\r\n");
        });
    }

    public async Task<IActionResult> OnGetPdfAsync(string? from, string? to, int? branch, int? category)
    {
        await BindFilterAsync(from, to, branch, category);
        var rep = await Svc<ReportService>().InventoryAsync(Filter);
        var pdf = new SimplePdf(CurrentUserName).Title("Inventory health").Text(Filter.Describe(await Db.Branches.ToListAsync(), await Db.Categories.ToListAsync()))
            .Heading("Items by status");
        foreach (var kv in rep.ByStatus) pdf.Text($"{Fmt.StatusText(kv.Key)}: {kv.Value}");
        pdf.Heading("Health by branch");
        var w = new[] { 20, 10, 10, 10, 12, 10 };
        pdf.Row(SimplePdf.Cells(w, "Branch", "Items", "Damaged", "Overdue", "Idle 12m", "Health"));
        foreach (var b in rep.Branches) pdf.Row(SimplePdf.Cells(w, b.Branch, b.Items.ToString(), b.Damaged.ToString(), b.Overdue.ToString(), b.Idle.ToString(), b.Good ? "Good" : "Watch"));
        pdf.Heading("Damaged items awaiting repair");
        foreach (var d in rep.Damaged.Take(50)) pdf.Text($"{d.Code} — {d.Title}");
        return File(pdf.Build(), "application/pdf", "inventory-health.pdf");
    }
}
