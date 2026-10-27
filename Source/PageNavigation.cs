namespace MooView;

// Keep the opened image at the start of its group. A partial prefix and tail
// are separate groups, so navigation never repeats images from the last view.
internal static class PageNavigation
{
    internal static int End(int start, int count, int size, int offset) => Math.Min(count, start == 0 && offset > 0 ? offset : start + size);
    internal static int Last(int count, int size, int offset) => count <= offset ? 0 : offset + Math.Max(0, (count - 1 - offset) / size) * size;
    internal static int? Neighbor(int start, int count, int size, int offset, int direction, bool loop)
    {
        if (count < 1) return null;
        if (direction > 0) { int next = End(start, count, size, offset); return next < count ? next : loop ? 0 : null; }
        if (start == 0) return loop ? Last(count, size, offset) : null;
        return Math.Max(0, start - size);
    }
}
