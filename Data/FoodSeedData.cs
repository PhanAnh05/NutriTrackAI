using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using NutriTrackAI.Models.Entities;

namespace NutriTrackAI.Data;

internal static class FoodSeedData
{
    private const string FileName = "NutriTrackAI_150_MonAn_ChuanHoa.csv";
    private static readonly Lazy<Dictionary<string, Row>> Catalogue = new(() =>
        Read(Path.Combine(AppContext.BaseDirectory, "Data", "Seed", FileName)));

    // Only these scalar properties are owned by the CSV. EF keeps IDs, visibility,
    // images, creation dates and navigation properties on existing rows.
    internal sealed record Values(string Name, string ServingName, decimal ServingSize, decimal Calories,
        decimal Protein, decimal Carb, decimal Fat, decimal Fiber, decimal Sugar, decimal Sodium,
        string Ingredients, string AllergenNotes, string HealthWarning);
    internal sealed record Row(string CategorySlug, string CategoryName, string CookingState, Values Food);

    internal static string? CookingState(string name) =>
        Catalogue.Value.GetValueOrDefault(Key(name))?.CookingState;

    private static string Key(string value) => value.Trim().Normalize();

    internal static Dictionary<string, Row> Read(string path)
    {
        using var parser = new TextFieldParser(path, new UTF8Encoding(false, true), true);
        parser.SetDelimiters(",");
        var headers = parser.ReadFields() ?? throw new InvalidDataException($"CSV trống: {path}");
        var columns = headers.Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
        string[] required = ["CategorySlug", "CategoryName", "CookingState", "Name", "ServingName", "ServingSize",
            "Calories", "Protein_g", "Carb_g", "Fat_g", "Fiber_g", "Sugar_g", "Sodium_mg",
            "Ingredients", "AllergenNotes", "HealthWarning"];
        foreach (var column in required)
            if (!columns.ContainsKey(column)) throw new InvalidDataException($"CSV thiếu cột {column}.");

        var rows = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
        var categoryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (!parser.EndOfData)
        {
            var line = parser.LineNumber;
            var fields = parser.ReadFields();
            if (fields == null || fields.Length != headers.Length)
                throw new InvalidDataException($"CSV dòng {line}: số cột không khớp tiêu đề.");
            string Cell(string name) => fields[columns[name]].Trim();
            string Required(string name) => Cell(name) is { Length: > 0 } value ? value :
                throw new InvalidDataException($"CSV dòng {line}: thiếu {name}.");
            decimal Number(string name)
            {
                if (decimal.TryParse(Cell(name), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
                    && (name != "ServingSize" || value > 0)) return value;
                throw new InvalidDataException($"CSV dòng {line}: {name} không hợp lệ.");
            }

            var food = new Values(Required("Name"), Required("ServingName"), Number("ServingSize"), Number("Calories"),
                Number("Protein_g"), Number("Carb_g"), Number("Fat_g"), Number("Fiber_g"), Number("Sugar_g"),
                Number("Sodium_mg"), Cell("Ingredients"), Cell("AllergenNotes"), Cell("HealthWarning"));
            var row = new Row(Required("CategorySlug"), Required("CategoryName"), Required("CookingState"), food);
            if (categoryNames.TryGetValue(row.CategorySlug, out var previous) && previous != row.CategoryName)
                throw new InvalidDataException($"CSV dòng {line}: tên danh mục không nhất quán cho {row.CategorySlug}.");
            categoryNames[row.CategorySlug] = row.CategoryName;
            if (!rows.TryAdd(Key(food.Name), row))
                throw new InvalidDataException($"CSV dòng {line}: trùng tên món {food.Name}.");
        }
        if (rows.Count == 0) throw new InvalidDataException("CSV không có món ăn.");
        return rows;
    }

    internal static async Task UpsertAsync(AppDbContext db)
    {
        var rows = Catalogue.Value.Values;
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var categories = await db.FoodCategories.OrderBy(c => c.Id).ToListAsync();
        var existing = (await db.Foods.ToListAsync()).ToLookup(f => Key(f.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var group in rows.GroupBy(r => r.CategorySlug, StringComparer.OrdinalIgnoreCase))
        {
            var slug = group.Key;
            var name = group.First().CategoryName;
            var category = categories.Find(c => string.Equals(Key(c.Slug), slug, StringComparison.OrdinalIgnoreCase))
                ?? categories.Find(c => string.Equals(Key(c.Name), name, StringComparison.OrdinalIgnoreCase));
            if (category == null)
            {
                category = new FoodCategory();
                db.FoodCategories.Add(category);
            }
            category.Slug = slug;
            category.Name = name;
            foreach (var row in group)
            {
                var key = Key(row.Food.Name);
                var matches = existing[key];
                if (!existing.Contains(key))
                {
                    var added = new Food { IsVegetarian = VegetarianSeedFoods.Contains(key) };
                    db.Foods.Add(added);
                    matches = [added];
                }
                foreach (var food in matches)
                {
                    food.Category = category;
                    db.Entry(food).CurrentValues.SetValues(row.Food);
                }
            }
        }
        await db.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
    }
}
