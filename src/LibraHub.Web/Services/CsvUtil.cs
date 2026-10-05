using System.Text;

namespace LibraHub.Services;

public static class CsvUtil
{
    public static string Escape(object? v)
    {
        var s = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    public static string Line(params object?[] cells) => string.Join(",", cells.Select(Escape));

    /// <summary>Minimal RFC-4180 parser (quoted cells, doubled quotes, embedded newlines).</summary>
    public static List<List<string>> Parse(TextReader reader)
    {
        var rows = new List<List<string>>();
        var row = new List<string>(); var cell = new StringBuilder(); bool quoted = false; int c;
        while ((c = reader.Read()) != -1)
        {
            var ch = (char)c;
            if (quoted)
            {
                if (ch == '"') { if (reader.Peek() == '"') { reader.Read(); cell.Append('"'); } else quoted = false; }
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && reader.Peek() == '\n') reader.Read();
                row.Add(cell.ToString()); cell.Clear();
                if (row.Any(x => x.Length > 0)) rows.Add(row);
                row = new List<string>();
            }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); if (row.Any(x => x.Length > 0)) rows.Add(row); }
        return rows;
    }
}

/// <summary>Streams a CSV to the response without buffering it (spec F7: "very large export – streamed").</summary>
public class CsvStreamResult(string fileName, Func<TextWriter, Task> write) : Microsoft.AspNetCore.Mvc.IActionResult
{
    public async Task ExecuteResultAsync(Microsoft.AspNetCore.Mvc.ActionContext context)
    {
        var r = context.HttpContext.Response;
        r.ContentType = "text/csv; charset=utf-8";
        r.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        await using var w = new StreamWriter(r.Body, new UTF8Encoding(true), 8192, leaveOpen: true);
        await write(w);
        await w.FlushAsync();
    }
}
