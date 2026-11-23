using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic.FileIO;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Services;
using Xunit;

namespace NutriTrackAI.Tests;

public class FoodCatalogueTests
{
    private static AppDbContext NewDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static FoodToolService Tools(AppDbContext db) =>
        new(db, new NutritionCalculatorService(), NullLogger<FoodToolService>.Instance, null!);

    [Fact]
    public async Task AllSourceRowsKeepTheirCategoryServingNutritionAndWarnings()
    {
        await using var db = NewDatabase();
        await FoodSeedData.UpsertAsync(db);
        db.ChangeTracker.Clear();
        var foods = await db.Foods.Include(f => f.Category).ToDictionaryAsync(f => f.Name);
        Assert.Equal(150, foods.Count);
        Assert.Equal(6, await db.FoodCategories.CountAsync());
        using var csv = new TextFieldParser(Path.Combine(QaApplication.Root, "Data", "Seed", "NutriTrackAI_150_MonAn_ChuanHoa.csv"));
        csv.SetDelimiters(",");
        var header = csv.ReadFields()!;
        while (!csv.EndOfData)
        {
            var row = header.Zip(csv.ReadFields()!).ToDictionary(p => p.First, p => p.Second);
            var f = foods[row["Name"]];
            Assert.Equal(row["CategorySlug"], f.Category.Slug);
            Assert.Equal(row["CategoryName"], f.Category.Name);
            Assert.Equal(row["ServingName"], f.ServingName);
            Assert.Equal(row["CookingState"], FoodSeedData.CookingState(f.Name));
            Assert.Equal(new[] { "ServingSize", "Calories", "Protein_g", "Carb_g", "Fat_g", "Fiber_g", "Sugar_g", "Sodium_mg" }
                .Select(key => decimal.Parse(row[key], CultureInfo.InvariantCulture)),
                new[] { f.ServingSize, f.Calories, f.Protein, f.Carb, f.Fat, f.Fiber, f.Sugar, f.Sodium });
            Assert.Equal(row["Ingredients"], f.Ingredients);
            Assert.Equal(row["AllergenNotes"], f.AllergenNotes);
            Assert.Equal(row["HealthWarning"], f.HealthWarning);
        }
    }

    [Fact]
    public async Task ReseedingKeepsIdsReferencesAndUnrelatedFoods()
    {
        await using var db = NewDatabase();
        var category = new FoodCategory { Id = 48, Name = "Món chính", Slug = "mon-chinh" };
        var match = new Food { Name = "  PHỞ BÒ TÁI ", Category = category, Calories = 100, IsHidden = true, ImageUrl = "/pho.jpg" };
        var old = new Food { Name = "Món cũ có nhật ký", Category = category, Calories = 123 };
        db.Foods.AddRange(match, old);
        foreach (var food in new[] { match, old })
        {
            db.Favorites.Add(new Favorite { Food = food, UserId = 1 });
            db.MealLogDetails.Add(new MealLogDetail { Food = food, MealLogId = 2, Calories = 112 });
            db.MealPlanItems.Add(new MealPlanItem { Food = food, MealPlanId = 3, Servings = 200 });
        }
        db.FoodCategories.Add(new FoodCategory { Name = "Tinh bột (Khô & Chín)", Slug = "custom-starch" });
        await db.SaveChangesAsync();
        var ids = new[] { match.Id, old.Id }.Order().ToArray();
        for (int run = 0; run < 2; run++)
        {
            db.ChangeTracker.Clear();
            await FoodSeedData.UpsertAsync(db);
            db.ChangeTracker.Clear();
            var updated = await db.Foods.Include(f => f.Category).SingleAsync(f => f.Id == match.Id);
            Assert.Equal("Phở bò tái", updated.Name);
            Assert.Equal(431m, updated.Calories);
            Assert.NotEqual(48, updated.CategoryId);
            Assert.Equal("mon-chinh-viet-nam", updated.Category.Slug);
            Assert.True(updated.IsHidden);
            Assert.Equal("/pho.jpg", updated.ImageUrl);
            var kept = await db.Foods.Include(f => f.Category).SingleAsync(f => f.Id == old.Id);
            Assert.Equal(123m, kept.Calories);
            Assert.Equal(48, kept.CategoryId);
            Assert.Equal("mon-chinh", kept.Category.Slug);
            Assert.Equal(151, await db.Foods.CountAsync());
            Assert.Equal(7, await db.FoodCategories.CountAsync());
            Assert.Equal(ids, await db.Favorites.OrderBy(f => f.FoodId).Select(x => x.FoodId).ToArrayAsync());
            Assert.Equal(ids, await db.MealLogDetails.Where(d => d.Calories == 112).OrderBy(d => d.FoodId).Select(d => d.FoodId).ToArrayAsync());
            Assert.Equal(ids, await db.MealPlanItems.Where(i => i.Servings == 200).OrderBy(i => i.FoodId).Select(i => i.FoodId).ToArrayAsync());
        }
    }

    [Fact]
    public async Task ChatRecognizesEveryDishAndFiltersTheExampleProfileAcrossTheCatalogue()
    {
        await using var db = NewDatabase();
        await FoodSeedData.UpsertAsync(db);
        var tool = Tools(db);
        var foods = await db.Foods.AsNoTracking().ToListAsync();
        var blocked = foods.Where(f => new[] { "tôm", "đậu phộng", "hành tây" }.Any(term =>
            $"{f.Ingredients}, {f.AllergenNotes}".Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(f => f.Id).ToHashSet();
        Assert.Equal(20, blocked.Count);
        foreach (var food in foods)
        {
            var mention = await tool.FindMentionedFoodAsync($"Tra cứu {food.Name} bao nhiêu calo?");
            Assert.Equal(food.Name, mention);
            Assert.Equal(food.Id, (await tool.SearchFoodsAsync(mention)).First().Id);
            var filtered = await tool.SearchFoodsAsync(mention, excludeAllergens: ["tom, lạc"], dislikedFoods: ["hanh tay"]);
            Assert.DoesNotContain(filtered, f => blocked.Contains(f.Id));
            Assert.Equal(!blocked.Contains(food.Id), filtered.Any(f => f.Id == food.Id));
        }

        var swap = await tool.GetSwapSuggestionsAsync("pho bo tai", DietType.Normal, ["tôm", "đậu phộng"], dislikedFoods: ["hành tây"]);
        Assert.Equal("Phở bò tái", swap.OriginalFoodName);
        Assert.NotEmpty(swap.AlternativeFoods);
        Assert.DoesNotContain(swap.AlternativeFoods, f => blocked.Contains(f.Id));
        var meal = await tool.SuggestMealAsync(2000, DietType.Vegetarian, ["tôm", "đậu phộng"], dislikedFoods: ["hành tây"]);
        Assert.Equal(3, meal.SuggestedItems.Select(f => f.Category).Distinct().Count());
        Assert.All(meal.SuggestedItems, f => Assert.True(foods.Single(x => x.Id == f.Id).IsVegetarian));
        Assert.DoesNotContain(meal.SuggestedItems, f => blocked.Contains(f.Id));
    }

    [Fact]
    public async Task SearchFiltersBeforeLimitingResults()
    {
        await using var db = NewDatabase();
        var category = new FoodCategory { Name = "Món chính", Slug = "mon-chinh" };
        for (var i = 1; i <= 45; i++)
            db.Foods.Add(new Food { Name = $"Món thử {i}", Category = category, Ingredients = i <= 35 ? "tôm khô" : "rau" });
        await db.SaveChangesAsync();
        var found = await Tools(db).SearchFoodsAsync("mon thu", excludeAllergens: ["shrimp"]);
        Assert.Equal(10, found.Count);
        Assert.All(found, item => Assert.Equal("rau", item.Ingredients));
    }
}
