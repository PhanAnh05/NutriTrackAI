namespace NutriTrackAI.Services;

internal static class AllergenGroups
{
    internal static (IReadOnlyList<string> Groups, bool HasUnknown) Normalize(IEnumerable<string> terms)
    {
        var groups = new HashSet<string>(StringComparer.Ordinal);
        var hasUnknown = false;
        foreach (var term in terms)
        {
            var group = ChatSafetyFilter.RemoveDiacritics(term.Trim().ToLowerInvariant()) switch
            {
                "dau phong" or "lac" or "peanut" or "peanuts" => "đậu phộng",
                "hat cay" or "hat dieu" or "hanh nhan" or "oc cho" or "tree nuts" => "hạt cây",
                "hai san" or "seafood" => "hải sản",
                "tom" or "cua" or "ghe" or "giap xac" or "shrimp" or "crab" or "crustaceans" => "giáp xác",
                "muc" or "oc" or "so" or "nhuyen the" or "molluscs" => "nhuyễn thể",
                "ca" or "fish" => "cá",
                "trung" or "trung ga" or "trung vit" or "egg" or "eggs" => "trứng",
                "sua" or "sua bo" or "milk" or "dairy" => "sữa",
                "dau nanh" or "dau tuong" or "soy" or "soya" => "đậu nành",
                "lua mi" or "wheat" => "lúa mì",
                "gluten" => "gluten",
                "me" or "vung" or "sesame" => "mè",
                _ => null
            };
            if (group == null) hasUnknown = true;
            else groups.Add(group);
        }
        return (groups.OrderBy(group => group, StringComparer.Ordinal).ToArray(), hasUnknown);
    }
}
