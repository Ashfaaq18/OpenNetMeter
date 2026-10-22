using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Services;

/// <summary>
/// Writes the self-contained HTML pages behind the "HTML report" export. The pages carry no scripts and load
/// nothing from the network, and every dynamic string goes through <see cref="Encode"/> because adapter
/// names are Wi-Fi SSIDs, which anyone nearby can choose.
/// </summary>
internal static class HtmlReport
{
    // WriteMeta colours the values behind these two labels like the app's download and upload brushes.
    public const string TotalDownloadLabel = "Total download";
    public const string TotalUploadLabel = "Total upload";

    private const string Css = """
        :root{--bg:#f4f6f8;--card:#fff;--text:#1c2127;--muted:#5f6b7a;--border:#dde2e8;--zebra:#f8f9fb;--hover:#eef1f5;--track:#e6eaef;--bar:#5b7fd6;--down:#4AA98C;--up:#E17717}
        @media (prefers-color-scheme:dark){:root{--bg:#14171b;--card:#1c2026;--text:#e7eaee;--muted:#98a3b1;--border:#2e343c;--zebra:#20252b;--hover:#262d35;--track:#2c323a;--bar:#7b9be8;--down:#5ec3a3;--up:#f29a3f}}
        *{box-sizing:border-box}
        body{margin:0;padding:24px 16px;background:var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif}
        main{max-width:960px;margin:0 auto}
        h1{margin:0 0 16px;font-size:24px}
        h2{margin:32px 0 12px;font-size:18px;overflow-wrap:anywhere}
        h3{margin:20px 0 8px;font-size:12px;letter-spacing:.05em;text-transform:uppercase;color:var(--muted)}
        .card{background:var(--card);border:1px solid var(--border);border-radius:8px;padding:16px 20px;box-shadow:0 1px 2px rgba(0,0,0,.06)}
        .meta{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:12px 24px;margin:0}
        .meta dt{font-size:12px;color:var(--muted)}
        .meta dd{margin:2px 0 0;font-size:15px;font-weight:600;overflow-wrap:anywhere}
        .meta .dl{color:var(--down)}
        .meta .ul{color:var(--up)}
        .scroll{overflow-x:auto}
        table{width:100%;border-collapse:collapse;background:var(--card);border:1px solid var(--border)}
        th,td{padding:8px 12px;text-align:left;border-bottom:1px solid var(--border);overflow-wrap:anywhere}
        th{font-size:12px;font-weight:600;color:var(--muted)}
        tbody tr:nth-child(even){background:var(--zebra)}
        tbody tr:last-child td{border-bottom:0}
        .num{text-align:right;white-space:nowrap;font-variant-numeric:tabular-nums}
        th.share,td.share{width:22%;min-width:90px}
        .track{display:block;height:8px;border-radius:4px;background:var(--track);overflow:hidden}
        .fill{display:block;height:100%;background:var(--bar)}
        .hint{margin:0 0 8px;font-size:12px;color:var(--muted)}
        .ptable{min-width:560px;background:var(--card);border:1px solid var(--border)}
        .prow{display:grid;grid-template-columns:minmax(0,3fr) repeat(3,minmax(72px,1fr)) minmax(90px,1.4fr);gap:0 12px;align-items:center;padding:6px 8px}
        .phead{font-size:12px;font-weight:600;color:var(--muted)}
        .pitem{border-top:1px solid var(--border)}
        .pitem:nth-child(odd){background:var(--zebra)}
        .pname{overflow-wrap:anywhere}
        .pitem>summary{list-style:none;cursor:pointer}
        .pitem>summary::-webkit-details-marker{display:none}
        .pitem>summary:hover{background:var(--hover)}
        .pitem>summary:focus-visible{outline:2px solid var(--bar);outline-offset:-2px}
        .pitem>summary .pname::before{content:"\25B8";display:inline-block;width:1em;color:var(--muted)}
        .pitem[open]>summary .pname::before{content:"\25BE"}
        .pstatic .pname{padding-left:1em}
        .pday{border-top:1px dotted var(--border);font-size:13px;color:var(--muted)}
        .pday .pname{padding-left:1em}
        .empty{color:var(--muted)}
        footer{margin-top:32px;font-size:12px;color:var(--muted)}
        @media print{
        :root{--bg:#fff;--card:#fff;--text:#000;--muted:#555;--border:#bbb;--zebra:#fff;--hover:#fff;--track:#fff;--bar:#555}
        body{padding:0}
        .card{box-shadow:none}
        tr{break-inside:avoid}
        thead{display:table-header-group}
        h2,h3{break-after:avoid}
        .track{border:1px solid #999}
        .hint{display:none}
        .ptable{min-width:0}
        .pitem{break-inside:avoid}
        .fill{-webkit-print-color-adjust:exact;print-color-adjust:exact}
        }
        """;

    public static string Encode(string value) => WebUtility.HtmlEncode(value);

    /// <summary>True for the extensions that select the HTML report (.html and .htm, any case).</summary>
    public static bool IsHtmlPath(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatDate(DateOnly date) =>
        date.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    public static string FormatDateTime(DateTime value) =>
        value.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + ", " + value.ToString("t", CultureInfo.CurrentCulture);

    public static void WriteDocumentStart(TextWriter w, string title)
    {
        var encodedTitle = Encode(title);
        w.WriteLine("<!DOCTYPE html>");
        w.WriteLine("<html lang=\"en\">");
        w.WriteLine("<head>");
        w.WriteLine("<meta charset=\"utf-8\">");
        w.WriteLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        // No scripts and no external loads: only the inline style block below is allowed.
        w.WriteLine("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\">");
        w.WriteLine($"<title>{encodedTitle}</title>");
        w.WriteLine("<style>");
        w.WriteLine(Css);
        w.WriteLine("</style>");
        w.WriteLine("</head>");
        w.WriteLine("<body>");
        w.WriteLine("<main>");
        w.WriteLine($"<h1>{encodedTitle}</h1>");
    }

    public static void WriteDocumentEnd(TextWriter w)
    {
        w.WriteLine($"<footer>Generated by OpenNetMeter on {Encode(FormatDateTime(DateTime.Now))}</footer>");
        w.WriteLine("</main>");
        w.WriteLine("</body>");
        w.WriteLine("</html>");
    }

    public static void WriteMeta(TextWriter w, IEnumerable<(string Label, string Value)> items)
    {
        w.WriteLine("<section class=\"card\">");
        w.WriteLine("<dl class=\"meta\">");
        foreach (var (label, value) in items)
        {
            var valueClass = label switch
            {
                TotalDownloadLabel => " class=\"dl\"",
                TotalUploadLabel => " class=\"ul\"",
                _ => string.Empty
            };
            w.WriteLine($"<div><dt>{Encode(label)}</dt><dd{valueClass}>{Encode(value)}</dd></div>");
        }
        w.WriteLine("</dl>");
        w.WriteLine("</section>");
    }

    public static void WriteNoTraffic(TextWriter w) =>
        w.WriteLine("<p class=\"empty\">No traffic recorded.</p>");

    /// <summary>
    /// Writes a Process | Download | Upload | Total | Share table, largest total first then by name.
    /// The share bar is each row's total relative to the largest row.
    /// </summary>
    public static void WriteProcessTable(TextWriter w, IEnumerable<(string Process, long Download, long Upload)> rows)
    {
        var sorted = rows
            .OrderByDescending(r => r.Download + r.Upload)
            .ThenBy(r => r.Process, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (sorted.Count == 0)
        {
            WriteNoTraffic(w);
            return;
        }

        long max = sorted[0].Download + sorted[0].Upload;

        w.WriteLine("<div class=\"scroll\">");
        w.WriteLine("<table>");
        w.WriteLine("<thead><tr><th>Process</th><th class=\"num\">Download</th><th class=\"num\">Upload</th><th class=\"num\">Total</th><th class=\"share\">Share</th></tr></thead>");
        w.WriteLine("<tbody>");
        foreach (var (process, download, upload) in sorted)
        {
            long total = download + upload;
            w.Write($"<tr><td>{Encode(process)}</td>");
            WriteSize(w, "td", download);
            WriteSize(w, "td", upload);
            WriteSize(w, "td", total);
            w.WriteLine($"<td class=\"share\"><div class=\"track\"><div class=\"fill\" style=\"width:{FormatShareWidth(total, max)}%\"></div></div></td></tr>");
        }
        w.WriteLine("</tbody>");
        w.WriteLine("</table>");
        w.WriteLine("</div>");
    }

    /// <summary>
    /// Like <see cref="WriteProcessTable"/>, but each process row expands to that process's usage per day.
    /// <c>details</c> cannot sit inside a table row, so this is a grid of <c>div</c> and <c>span</c> rows;
    /// it needs no scripts. Days without traffic are left out, and a process with no days is not expandable.
    /// </summary>
    public static void WriteExpandableProcessTable(
        TextWriter w,
        IEnumerable<(string Process, long Download, long Upload, IReadOnlyList<(DateOnly Date, long Download, long Upload)> Days)> rows)
    {
        var sorted = rows
            .OrderByDescending(r => r.Download + r.Upload)
            .ThenBy(r => r.Process, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (sorted.Count == 0)
        {
            WriteNoTraffic(w);
            return;
        }

        long max = sorted[0].Download + sorted[0].Upload;

        w.WriteLine("<p class=\"hint\">Click a process to see its usage per day.</p>");
        w.WriteLine("<div class=\"scroll\">");
        w.WriteLine("<div class=\"ptable\">");
        w.WriteLine("<div class=\"prow phead\"><span>Process</span><span class=\"num\">Download</span><span class=\"num\">Upload</span><span class=\"num\">Total</span><span class=\"share\">Share</span></div>");
        foreach (var (process, download, upload, days) in sorted)
        {
            var activeDays = days
                .Where(d => d.Download != 0 || d.Upload != 0)
                .OrderBy(d => d.Date)
                .ToList();

            if (activeDays.Count == 0)
            {
                w.Write("<div class=\"pitem\"><div class=\"prow pstatic\">");
                WriteProcessCells(w, process, download, upload, max);
                w.WriteLine("</div></div>");
                continue;
            }

            w.WriteLine("<details class=\"pitem\">");
            w.Write("<summary class=\"prow\">");
            WriteProcessCells(w, process, download, upload, max);
            w.WriteLine("</summary>");
            foreach (var (date, dayDownload, dayUpload) in activeDays)
            {
                w.Write($"<div class=\"prow pday\"><span class=\"pname\">{Encode(FormatDate(date))}</span>");
                WriteSize(w, "span", dayDownload);
                WriteSize(w, "span", dayUpload);
                WriteSize(w, "span", dayDownload + dayUpload);
                w.WriteLine("<span></span></div>");
            }
            w.WriteLine("</details>");
        }
        w.WriteLine("</div>");
        w.WriteLine("</div>");
    }

    /// <summary>
    /// Writes a Date | Download | Upload | Total table, oldest day first.
    /// </summary>
    public static void WriteDayTable(TextWriter w, IEnumerable<(DateOnly Date, long Download, long Upload)> rows)
    {
        var sorted = rows.OrderBy(r => r.Date).ToList();
        if (sorted.Count == 0)
        {
            WriteNoTraffic(w);
            return;
        }

        w.WriteLine("<div class=\"scroll\">");
        w.WriteLine("<table>");
        w.WriteLine("<thead><tr><th>Date</th><th class=\"num\">Download</th><th class=\"num\">Upload</th><th class=\"num\">Total</th></tr></thead>");
        w.WriteLine("<tbody>");
        foreach (var (date, download, upload) in sorted)
        {
            w.Write($"<tr><td>{Encode(FormatDate(date))}</td>");
            WriteSize(w, "td", download);
            WriteSize(w, "td", upload);
            WriteSize(w, "td", download + upload);
            w.WriteLine("</tr>");
        }
        w.WriteLine("</tbody>");
        w.WriteLine("</table>");
        w.WriteLine("</div>");
    }

    /// <summary>
    /// The width of a share bar as a percentage with one decimal ("50.0"), clamped to 0-100.
    /// Only this number ever ends up in a style attribute.
    /// </summary>
    internal static string FormatShareWidth(long total, long max)
    {
        double percent = max > 0 ? (double)total / max * 100 : 0;
        return Math.Clamp(percent, 0, 100).ToString("0.0", CultureInfo.InvariantCulture);
    }

    // The name, the three sizes and the share bar of one row of the expandable table.
    private static void WriteProcessCells(TextWriter w, string process, long download, long upload, long max)
    {
        long total = download + upload;
        w.Write($"<span class=\"pname\">{Encode(process)}</span>");
        WriteSize(w, "span", download);
        WriteSize(w, "span", upload);
        WriteSize(w, "span", total);
        w.Write($"<span class=\"share\"><span class=\"track\"><span class=\"fill\" style=\"width:{FormatShareWidth(total, max)}%\"></span></span></span>");
    }

    // The readable size is the cell text; hovering shows the exact byte count.
    private static void WriteSize(TextWriter w, string tag, long bytes)
    {
        var exact = bytes.ToString("N0", CultureInfo.InvariantCulture) + " bytes";
        w.Write($"<{tag} class=\"num\" title=\"{Encode(exact)}\">{Encode(ByteSizeFormatter.FormatBytes(bytes))}</{tag}>");
    }
}
