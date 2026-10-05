using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Reports;

/// <summary>Mockup 16 – Borrowing statistics.</summary>
public class BorrowingModel : ReportPageBase
{
    public BorrowingReport R { get; private set; } = null!;
    public async Task OnGetAsync(string? from, string? to, int? branch, int? category)
    {
        await BindFilterAsync(from, to, branch, category);
        R = await Svc<ReportService>().BorrowingAsync(Filter);
    }

    public async Task<IActionResult> OnGetCsvAsync(string? from, string? to, int? branch, int? category)
    {
        await BindFilterAsync(from, to, branch, category);
        var loans = await Svc<ReportService>().LoanQuery(Filter).OrderBy(l => l.BorrowedAt).ToListAsync();
        return new CsvStreamResult("borrowing-statistics.csv", async w =>
        {
            await w.WriteAsync(CsvUtil.Line("BorrowedAt", "DueAt", "ReturnedAt", "Item", "Branch", "Category", "OnTime") + "\r\n");
            foreach (var l in loans)
                await w.WriteAsync(CsvUtil.Line(l.BorrowedAt, l.DueAt, l.ReturnedAt, l.Item.Title, l.Item.CurrentBranch.Name, l.Item.Category.Name,
                    l.ReturnedAt == null ? "" : (l.ReturnedAt.Value.Date <= l.DueAt.Date).ToString()) + "\r\n");
        });
    }

    public async Task<IActionResult> OnGetPdfAsync(string? from, string? to, int? branch, int? category)
    {
        await BindFilterAsync(from, to, branch, category);
        var pdf = new SimplePdf(CurrentUserName).Title("Borrowing statistics").Text(Filter.Describe(await Db.Branches.ToListAsync(), await Db.Categories.ToListAsync()))
            .Heading("Summary")
            .Text($"Total loans: {R.TotalLoans}    Active borrowers: {R.ActiveBorrowers}    Avg loan length: {R.AvgLoanDays} days    On-time return: {R.OnTimePct}%")
            .Heading("Loans per month");
        foreach (var m in R.PerMonth) pdf.Text($"{m.Label}: {m.Count}");
        pdf.Heading("Top borrowed items");
        foreach (var t in R.Top) pdf.Text($"{t.Title} — {t.Loans}");
        return File(pdf.Build(), "application/pdf", "borrowing-statistics.pdf");
    }
}
