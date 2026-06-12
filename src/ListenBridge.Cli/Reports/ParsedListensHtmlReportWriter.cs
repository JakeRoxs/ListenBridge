using System.Net;
using System.Text;
using ListenBridge.Core.Domain;

namespace ListenBridge.Cli.Reports;

public sealed class ParsedListensHtmlReportWriter
{
    public Task WriteHtmlReportAsync(string outputPath, IReadOnlyList<Listen> listens, string sourcePath)
    {
        if (outputPath is null)
        {
            throw new ArgumentNullException(nameof(outputPath));
        }

        var html = BuildHtmlReport(listens, sourcePath);
        ReportOutputDirectory.EnsureParentDirectoryExists(outputPath);
        return File.WriteAllTextAsync(outputPath, html, Encoding.UTF8);
    }

    public string BuildHtmlReport(IReadOnlyList<Listen> listens, string sourcePath)
    {
        if (listens is null)
        {
            throw new ArgumentNullException(nameof(listens));
        }

        static string Encode(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
        static string BuildOriginCell(Uri? originUrl)
        {
            if (originUrl is null)
            {
                return string.Empty;
            }

            if (!string.Equals(originUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(originUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var href = Encode(originUrl.ToString());
            return $"<a href=\"{href}\" target=\"_blank\">{href}</a>";
        }

        const string SummaryCardCloser = "</span></div>";
        const string TableCellStart = "          <td>";
        const string TableCellEnd = "</td>";

        var total = listens.Count;
        var first = listens.Count == 0 ? string.Empty : listens.Min(l => l.ListenedAt).ToString("u");
        var last = listens.Count == 0 ? string.Empty : listens.Max(l => l.ListenedAt).ToString("u");
        var title = Encode("YouTube Music Parsed Listens");
        var source = Encode(sourcePath);

        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">\n<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">\n  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n  <title>" + title + "</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine("    body { font-family: Inter, system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; margin: 0; padding: 0; background: #f4f6f8; color: #111; }");
        builder.AppendLine("    .page { max-width: 1400px; margin: 0 auto; padding: 24px; }");
        builder.AppendLine("    header { background: white; border-radius: 16px; padding: 24px; box-shadow: 0 16px 50px rgba(15,23,42,.08); margin-bottom: 24px; }");
        builder.AppendLine("    header h1 { margin: 0 0 12px; font-size: 2rem; }");
        builder.AppendLine("    .summary { display: grid; grid-template-columns: repeat(auto-fit,minmax(180px,1fr)); gap: 12px; margin-top: 16px; }");
        builder.AppendLine("    .summary .card { background:#f8fafc; border-radius:14px; padding:16px; box-shadow: inset 0 0 0 1px rgba(148,163,184,.16); }");
        builder.AppendLine("    .summary .card strong { display:block; font-size: .85rem; color:#475569; margin-bottom:.5rem; }");
        builder.AppendLine("    .summary .card span { font-size:1.35rem; font-weight:700; color:#0f172a; }");
        builder.AppendLine("    .actions { display:flex; flex-wrap:wrap; gap:12px; align-items:center; margin-bottom: 16px; }");
        builder.AppendLine("    .actions input { flex:1 1 280px; min-width:0; padding:12px 14px; font-size:1rem; border-radius:12px; border:1px solid #cbd5e1; }");
        builder.AppendLine("    .actions p { margin:0; color:#475569; }");
        builder.AppendLine("    table { width:100%; border-collapse:collapse; background:white; border-radius:16px; overflow:hidden; box-shadow: 0 16px 50px rgba(15,23,42,.08); }");
        builder.AppendLine("    th, td { padding: 14px 16px; text-align:left; border-bottom:1px solid #e2e8f0; }");
        builder.AppendLine("    th { background:#f8fafc; cursor:pointer; position:sticky; top:0; z-index:1; }");
        builder.AppendLine("    th:hover { background:#e2e8f0; }");
        builder.AppendLine("    tr:hover td { background:#f8fafc; }");
        builder.AppendLine("    a { color:#2563eb; text-decoration:none; }");
        builder.AppendLine("    a:hover { text-decoration:underline; }");
        builder.AppendLine("    .small { font-size:.95rem; color:#475569; }");
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>\n<body>");
        builder.AppendLine("  <div class=\"page\">\n    <header>");
        builder.AppendLine("      <h1>" + title + "</h1>");
        builder.AppendLine("      <p class=\"small\">A parsed YouTube Music listen export with interactive sorting and search.</p>");
        builder.AppendLine("      <div class=\"summary\">");
        builder.AppendLine("        <div class=\"card\"><strong>Total listens</strong><span>" + total + SummaryCardCloser);
        builder.AppendLine("        <div class=\"card\"><strong>First listen</strong><span>" + Encode(first) + SummaryCardCloser);
        builder.AppendLine("        <div class=\"card\"><strong>Last listen</strong><span>" + Encode(last) + SummaryCardCloser);
        builder.AppendLine("        <div class=\"card\"><strong>Source file</strong><span>" + source + SummaryCardCloser);
        builder.AppendLine("      </div>\n    </header>");
        builder.AppendLine("    <div class=\"actions\">");
        builder.AppendLine("      <input id=\"search\" type=\"text\" placeholder=\"Filter by artist, track, or URL\" oninput=\"filterTable()\">\n      <p>Click any column header to sort.</p>");
        builder.AppendLine("    </div>");
        builder.AppendLine("    <table id=\"listens\">\n      <thead>\n        <tr>");
        builder.AppendLine("          <th onclick=\"sortTable(0)\">Date</th>");
        builder.AppendLine("          <th onclick=\"sortTable(1)\">Artist</th>");
        builder.AppendLine("          <th onclick=\"sortTable(2)\">Track</th>");
        builder.AppendLine("          <th onclick=\"sortTable(3)\">Origin</th>");
        builder.AppendLine("        </tr>\n      </thead>\n      <tbody>");

        foreach (var listen in listens)
        {
            var date = Encode(listen.ListenedAt.ToString("u"));
            var artist = Encode(listen.ArtistName);
            var track = Encode(listen.TrackName);
            var originHtml = BuildOriginCell(listen.OriginUrl);

            builder.AppendLine("        <tr>");
            builder.AppendLine(TableCellStart + date + TableCellEnd);
            builder.AppendLine(TableCellStart + artist + TableCellEnd);
            builder.AppendLine(TableCellStart + track + TableCellEnd);
            builder.AppendLine(TableCellStart + originHtml + TableCellEnd);
            builder.AppendLine("        </tr>");
        }

        builder.AppendLine("      </tbody>\n    </table>");
        builder.AppendLine("  </div>\n  <script>");
        builder.AppendLine("    function sortTable(columnIndex) {\n      const table = document.getElementById('listens');\n      const tbody = table.tBodies[0];\n      const rows = Array.from(tbody.rows);\n      const currentOrder = table.getAttribute('data-sort') || 'desc';\n      const newOrder = currentOrder === 'desc' ? 'asc' : 'desc';\n      rows.sort((a, b) => {\n        const aText = a.cells[columnIndex].innerText.trim().toLowerCase();\n        const bText = b.cells[columnIndex].innerText.trim().toLowerCase();\n        if (!isNaN(Date.parse(aText)) && !isNaN(Date.parse(bText))) {\n          return new Date(aText) - new Date(bText);\n        }\n        if (aText < bText) return -1;\n        if (aText > bText) return 1;\n        return 0;\n      });\n      if (newOrder === 'desc') rows.reverse();\n      table.setAttribute('data-sort', newOrder);\n      tbody.innerHTML = '';\n      rows.forEach(row => tbody.appendChild(row));\n    }\n");
        builder.AppendLine("    function filterTable() {\n      const query = document.getElementById('search').value.toLowerCase();\n      const table = document.getElementById('listens');\n      for (const row of table.tBodies[0].rows) {\n        const text = row.innerText.toLowerCase();\n        row.style.display = text.includes(query) ? '' : 'none';\n      }\n    }\n");
        builder.AppendLine("  </script>\n</body>\n</html>");

        return builder.ToString();
    }
}
