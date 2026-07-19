using WCG.PaintingPictures.Web.Server.Models;

namespace WCG.PaintingPictures.Web.Server.Services;

public sealed class JustifiedRow
{
    public required IReadOnlyList<PortfolioItem> Items { get; init; }
    public double AspectSum { get; init; }
    public bool IsLastRow { get; init; }
}

public static class JustifiedGridLayout
{
    public const int GapPx = 2;
    public const int TargetRowHeight = 240;

    public static IReadOnlyList<JustifiedRow> BuildRows(IReadOnlyList<PortfolioItem> items, int containerWidth = 1200)
    {
        var rows = new List<JustifiedRow>();
        var current = new List<PortfolioItem>();
        var index = 0;

        while (index < items.Count)
        {
            current.Add(items[index++]);

            var aspectSum = current.Sum(GetAspectRatio);
            var gaps = GapPx * (current.Count - 1);
            var rowWidth = aspectSum * TargetRowHeight + gaps;

            if (rowWidth >= containerWidth || index >= items.Count)
            {
                var isLast = index >= items.Count;
                rows.Add(new JustifiedRow
                {
                    Items = current.ToList(),
                    AspectSum = aspectSum,
                    IsLastRow = isLast
                });
                current = [];
            }
        }

        return rows;
    }

    public static double GetAspectRatio(PortfolioItem item) =>
        item.HasImage && item.ImageHeight > 0
            ? (double)item.ImageWidth / item.ImageHeight
            : 1.0;
}
