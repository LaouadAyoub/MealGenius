using MealGeniusBackend.DataAcess;

namespace MealGeniusBackend.Helpers;

public static class GroceryImageMatcher
{
    public static GroceryItem? Find(IEnumerable<GroceryItem> catalogue, string name, IEnumerable<string>? aliases)
    {
        var names = new HashSet<string>(aliases ?? [], StringComparer.OrdinalIgnoreCase) { name };
        return catalogue.FirstOrDefault(item =>
            (!string.IsNullOrWhiteSpace(item.CompressedImageUrl) || !string.IsNullOrWhiteSpace(item.ImageUrl))
            && (names.Contains(item.Name) || (item.SimilarNames ?? []).Any(names.Contains)));
    }

    public static List<string> MergeAliases(IEnumerable<string>? existing, IEnumerable<string>? additions) =>
        (existing ?? []).Concat(additions ?? []).Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
