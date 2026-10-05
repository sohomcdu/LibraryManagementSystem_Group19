using System.Text;

namespace LibraHub.Infrastructure;

/// <summary>Real Code 39 barcode rendered as inline SVG (scannable by desk/kiosk scanners – mockup 5 #2).</summary>
public static class Barcode
{
    static readonly Dictionary<char, string> P = new()
    {
        ['0'] = "nnnwwnwnn", ['1'] = "wnnwnnnnw", ['2'] = "nnwwnnnnw", ['3'] = "wnwwnnnnn", ['4'] = "nnnwwnnnw",
        ['5'] = "wnnwwnnnn", ['6'] = "nnwwwnnnn", ['7'] = "nnnwnnwnw", ['8'] = "wnnwnnwnn", ['9'] = "nnwwnnwnn",
        ['A'] = "wnnnnwnnw", ['B'] = "nnwnnwnnw", ['C'] = "wnwnnwnnn", ['D'] = "nnnnwwnnw", ['E'] = "wnnnwwnnn",
        ['F'] = "nnwnwwnnn", ['G'] = "nnnnnwwnw", ['H'] = "wnnnnwwnn", ['I'] = "nnwnnwwnn", ['J'] = "nnnnwwwnn",
        ['K'] = "wnnnnnnww", ['L'] = "nnwnnnnww", ['M'] = "wnwnnnnwn", ['N'] = "nnnnwnnww", ['O'] = "wnnnwnnwn",
        ['P'] = "nnwnwnnwn", ['Q'] = "nnnnnnwww", ['R'] = "wnnnnnwwn", ['S'] = "nnwnnnwwn", ['T'] = "nnnnwnwwn",
        ['U'] = "wwnnnnnnw", ['V'] = "nwwnnnnnw", ['W'] = "wwwnnnnnn", ['X'] = "nwnnwnnnw", ['Y'] = "wwnnwnnnn",
        ['Z'] = "nwwnwnnnn", ['-'] = "nwnnnnwnw", ['*'] = "nwnnwnwnn"
    };

    public static string Svg(string text, int height = 56)
    {
        var data = "*" + text.ToUpperInvariant().Where(P.ContainsKey).Aggregate("", (a, c) => a + c) + "*";
        var sb = new StringBuilder();
        double x = 0; const double n = 2, w = 5;
        foreach (var ch in data)
        {
            var pat = P[ch];
            for (int i = 0; i < 9; i++)
            {
                var width = pat[i] == 'w' ? w : n;
                if (i % 2 == 0) sb.Append($"<rect x=\"{x:0.#}\" y=\"0\" width=\"{width}\" height=\"{height}\" fill=\"#111\"/>");
                x += width;
            }
            x += n; // inter-character gap
        }
        return $"<svg class=\"barcode\" viewBox=\"0 0 {x:0} {height}\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"Barcode {text}\" xmlns=\"http://www.w3.org/2000/svg\">{sb}</svg>";
    }
}
