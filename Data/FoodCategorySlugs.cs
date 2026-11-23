namespace NutriTrackAI.Data;

internal static class FoodCategorySlugs
{
    internal const string Main = "mon-chinh-viet-nam";
    internal const string Starch = "tinh-bot-song-chin";
    internal const string Protein = "dam-song-chin";
    internal const string Vegetable = "rau-cu-canh-xao";
    internal const string Fruit = "trai-cay-hat-an-vat";
    internal const string Drink = "do-uong-gia-vi";

    private static string? Legacy(string slug) => slug switch
    {
        Main => "mon-chinh",
        Starch => "tinh-bot",
        Protein => "thit-ca-dam",
        Vegetable => "rau-cu",
        Fruit => "trai-cay",
        Drink => "do-uong-an-vat",
        _ => null
    };

    internal static bool Matches(string actual, string canonical) =>
        actual == canonical || actual == Legacy(canonical);
}
