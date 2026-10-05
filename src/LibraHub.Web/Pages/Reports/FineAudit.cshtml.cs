using LibraHub.Domain;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Reports;

/// <summary>Mockup 17 – Fine revenue audit (Admin sees patron names; Manager sees a hidden-name ledger).</summary>
public class FineAuditModel : ReportPageBase
{
    public FineReport R { get; private set; } = null!;
    public async Task OnGetAsync(string? from, string? to, int? branch, int? category)
    {
        await BindFilterAsync(from, to, branch, category);
        R = await Svc<ReportService>().FinesAsync(Filter, IsAdmin);
    }

    public async Task<IActionResult> OnGetCsvAsync(string? from, string? to, int? branch, int? category)
    {
        await BindFilterAsync(from, to, branch, category);
        var rep = await Svc<ReportService>().FinesAsync(Filter, IsAdmin);
        return new CsvStreamResult("fine-revenue-audit.csv", async w =>
        {
            await w.WriteAsync(CsvUtil.Line("Date", "Patron", "Item", "DaysLate", "Amount", "Status", "Desk") + "\r\n");
            foreach (var row in rep.Rows)
                await w.WriteAsync(CsvUtil.Line(row.Date, row.Patron, row.Item, row.DaysLate, Fmt.Money(row.AmountCents), row.Status, row.Desk) + "\r\n");
        });
    }

    public async Task<IActionResult> OnGetPdfAsync(string? from, string? to, int? branch, int? category)
    {
        await BindFilterAsync(from, to, branch, category);
        var rep = await Svc<ReportService>().FinesAsync(Filter, IsAdmin);
        var pdf = new SimplePdf(CurrentUserName).Title("Fine revenue audit").Text(Filter.Describe(await Db.Branches.ToListAsync(), await Db.Categories.ToListAsync()))
            .Heading("Summary")
            .Text($"Issued {Fmt.Money(rep.Issued)}   Collected {Fmt.Money(rep.Collected)}   Outstanding {Fmt.Money(rep.Outstanding)}   Waived {Fmt.Money(rep.Waived)}")
            .Text(rep.Reconciled ? "Reconciled: collected + outstanding + waived = issued." : "NOT reconciled — check the ledger.")
            .Heading("Ledger");
        var w = new[] { 12, 18, 24, 6, 10, 12 };
        pdf.Row(SimplePdf.Cells(w, "Date", "Patron", "Item", "Late", "Amount", "Status"));
        foreach (var row in rep.Rows.Take(200)) pdf.Row(SimplePdf.Cells(w, Fmt.Day(row.Date), row.Patron, row.Item, row.DaysLate.ToString(), Fmt.Money(row.AmountCents), row.Status.ToString()));
        return File(pdf.Build(), "application/pdf", "fine-revenue-audit.pdf");
    }
}
