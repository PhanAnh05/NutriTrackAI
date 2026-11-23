using System.Text.RegularExpressions;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;

namespace NutriTrackAI.Services;

internal static class FoodText
{
    private static readonly Regex Separators = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);
    internal static string Words(string? text) =>
        Separators.Replace((text ?? "").Normalize().ToLowerInvariant(), " ").Trim();
    internal static string SearchKey(string? text) => ChatSafetyFilter.RemoveDiacritics(Words(text));
    internal static bool ContainsWords(string text, string phrase) =>
        phrase.Length > 0 && (" " + text + " ").Contains(" " + phrase + " ", StringComparison.Ordinal);

    internal static bool CanServe(Food food)
    {
        var state = FoodSeedData.CookingState(food.Name);
        return state is not ("Sống (Nguyên liệu)" or "Sống (Khô)" or "Khô (Ngâm nước)" or "Nguyên liệu")
            && state?.StartsWith("Gia vị", StringComparison.Ordinal) != true;
    }
}

internal sealed class FoodRestrictions
{
    private static readonly Regex OmittedInName = new(@"\(không\s+[^)]*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly (string Label, string[] Aliases)[] allergies;
    private readonly string[] dislikes;

    internal static string[] Split(string? text) =>
        (text ?? "").Split([',', ';', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    internal FoodRestrictions(IEnumerable<string>? allergyTerms, IEnumerable<string>? dislikedFoods = null)
    {
        allergies = (allergyTerms ?? []).SelectMany(Split).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(term => (term, Aliases(term))).ToArray();
        dislikes = (dislikedFoods ?? []).SelectMany(Split).SelectMany(Aliases).Distinct().ToArray();
    }

    private static string[] Aliases(string term)
    {
        var key = FoodText.SearchKey(term);
        string[] words = key switch
        {
            "tom" or "tep" or "shrimp" or "prawn" => ["tôm", "tép", "mắm ruốc", "shrimp", "prawn"],
            "dau phong" or "lac" or "peanut" or "peanuts" => ["đậu phộng", "lạc", "peanut", "peanuts"],
            "hanh tay" or "onion" => ["hành tây", "onion"],
            "giap xac" or "crustaceans" => ["giáp xác", "tôm", "tép", "mắm ruốc", "cua", "ghẹ", "shrimp", "crab"],
            "hai san" or "seafood" => ["hải sản", "giáp xác", "nhuyễn thể", "tôm", "tép", "cua", "ghẹ", "mực", "sò", "ốc", "hàu", "cá", "dầu hào", "nước mắm"],
            "cua" or "crab" => ["cua", "crab"],
            "nhuyen the" or "molluscs" => ["nhuyễn thể", "mực", "sò", "ốc", "hàu", "dầu hào"],
            "ca" or "fish" => ["cá", "nước mắm", "fish"],
            "sua" or "sua bo" or "milk" or "dairy" => ["sữa", "whey", "milk", "dairy"],
            "trung" or "trung ga" or "trung vit" or "egg" or "eggs" => ["trứng", "egg", "eggs"],
            "dau nanh" or "dau tuong" or "soy" or "soya" => ["đậu nành", "đậu tương", "đậu phụ", "đậu hũ", "nước tương", "xì dầu", "soy", "soya"],
            "hat cay" or "tree nuts" => ["hạt cây", "hạt điều", "hạnh nhân", "óc chó", "tree nuts"],
            "lua mi" or "wheat" => ["lúa mì", "bột mì", "bánh mì", "wheat"],
            "gluten" => ["gluten", "lúa mì", "bột mì", "bánh mì"],
            "me" or "vung" or "sesame" when FoodText.Words(term) != "me" => ["mè", "vừng", "sesame"],
            _ => [FoodText.Words(term)]
        };
        // Match unaccented entries too, without folding the food itself:
        // "lạc" and "lắc", or "cá" and "cà", are different ingredients.
        return words.Concat(words.Where(w => w != "mè").Select(ChatSafetyFilter.RemoveDiacritics))
            .Where(w => w.Length > 0).Distinct().ToArray();
    }

    private static string IngredientsAndLabels(Food food)
    {
        var name = OmittedInName.Replace(food.Name, " ");
        var words = FoodText.Words($"{name} | {food.Ingredients} | {food.AllergenNotes}");
        // Soy milk is indexed under soy. A separate milk warning still matches.
        return words.Replace("sữa đậu nành", "đậu nành", StringComparison.Ordinal);
    }

    internal List<string> MatchedAllergies(Food food)
    {
        var text = IngredientsAndLabels(food);
        return allergies.Where(a => a.Aliases.Any(word => FoodText.ContainsWords(text, word))).Select(a => a.Label).ToList();
    }

    internal bool Allows(Food food)
    {
        if (allergies.Length == 0 && dislikes.Length == 0) return true;
        var text = IngredientsAndLabels(food);
        if (allergies.Any(a => a.Aliases.Any(word => FoodText.ContainsWords(text, word)))) return false;
        return !dislikes.Any(word => FoodText.ContainsWords(text, word));
    }
}
