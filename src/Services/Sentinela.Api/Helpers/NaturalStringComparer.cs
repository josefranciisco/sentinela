namespace Sentinela.Api.Helpers;

/// <summary>
/// Ordenação natural: Mobi-01, Mobi-02, Mobi-10 (não Mobi-01, Mobi-10, Mobi-02).
/// </summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var ix = 0;
        var iy = 0;

        while (ix < x.Length && iy < y.Length)
        {
            if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
            {
                long nx = 0;
                long ny = 0;

                while (ix < x.Length && char.IsDigit(x[ix]))
                {
                    nx = nx * 10 + (x[ix] - '0');
                    ix++;
                }

                while (iy < y.Length && char.IsDigit(y[iy]))
                {
                    ny = ny * 10 + (y[iy] - '0');
                    iy++;
                }

                if (nx != ny)
                    return nx.CompareTo(ny);

                continue;
            }

            var cx = char.ToUpperInvariant(x[ix]);
            var cy = char.ToUpperInvariant(y[iy]);
            if (cx != cy)
                return cx.CompareTo(cy);

            ix++;
            iy++;
        }

        return (x.Length - ix).CompareTo(y.Length - iy);
    }
}
