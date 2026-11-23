using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public class FoodToolService : IFoodToolService
{
    private readonly AppDbContext _context;
    private readonly INutritionCalculatorService _nutritionCalculator;
    private readonly ILogger<FoodToolService> _logger;
    private readonly CalorieBudgetService _budgets;

    public FoodToolService(
        AppDbContext context,
        INutritionCalculatorService nutritionCalculator,
        ILogger<FoodToolService> logger, CalorieBudgetService budgets)
    {
        _context = context;
        _nutritionCalculator = nutritionCalculator;
        _logger = logger;
        _budgets = budgets;
    }

    public async Task<TodayNutritionDto> GetTodayNutritionAsync(int userId, DateTime? date = null)
    {
        var targetDate = (date ?? GoalPolicy.Today).Date;

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        var budget = profile == null ? null : await _budgets.GetAsync(userId);
        var dailyTarget = budget?.TargetCalories ?? 2000;

        var mealLogs = await _context.MealLogs
            .Include(m => m.Details)
                .ThenInclude(d => d.Food)
            .Where(m => m.UserId == userId && m.LoggedOn == targetDate)
            .ToListAsync();

        var allDetails = mealLogs.SelectMany(m => m.Details).ToList();
        var consumedCal = allDetails.Sum(d => d.Calories);
        var protein = allDetails.Sum(d => d.Protein);
        var carb = allDetails.Sum(d => d.Carb);
        var fat = allDetails.Sum(d => d.Fat);
        var fiber = allDetails.Sum(d => d.Fiber);
        var sugar = allDetails.Sum(d => d.Sugar);
        var sodium = allDetails.Sum(d => d.Sodium);

        var burnedCal = await _context.ExerciseLogs
            .Where(e => e.UserId == userId && e.LoggedOn == targetDate)
            .SumAsync(e => e.CaloriesBurned);

        var waterMl = await _context.WaterLogs
            .Where(w => w.UserId == userId && w.LoggedOn == targetDate)
            .SumAsync(w => w.AmountMl);

        var remainingCal = GoalPolicy.RemainingCalories(dailyTarget, consumedCal, burnedCal);

        var mealsList = allDetails.Select(d =>
            $"{d.MealLog.MealType}: {d.Food.Name} ({d.Servings:0.##} {d.Food.ServingName}, {d.Calories:N0} kcal)"
        ).ToList();

        return new TodayNutritionDto
        {
            Date = targetDate,
            DailyCalorieTarget = dailyTarget,
            CaloriesConsumed = consumedCal,
            CaloriesBurned = burnedCal,
            ExerciseCreditApplied = GoalPolicy.ExerciseCredit(burnedCal),
            RemainingCalories = remainingCal,
            Protein = protein,
            Carb = carb,
            Fat = fat,
            Fiber = fiber,
            Sugar = sugar,
            Sodium = sodium,
            MaxSodiumMg = budget?.Health.Constraints.MaxSodiumMg,
            RecommendedMacros = profile != null && budget != null ? _budgets.GetMacros(profile, budget) : null,
            WaterMl = waterMl,
            MealsLogged = mealsList
        };
    }

    public async Task<RemainingCaloriesDto> GetRemainingCaloriesAsync(int userId, DateTime? date = null)
    {
        var targetDate = (date ?? GoalPolicy.Today).Date;

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        var activeGoal = await _context.Goals.FirstOrDefaultAsync(g => g.UserId == userId && g.IsActive);

        var dailyTarget = profile == null ? 2000 : (await _budgets.GetAsync(userId)).TargetCalories;

        var consumedCal = await _context.MealLogDetails
            .Where(d => d.MealLog.UserId == userId && d.MealLog.LoggedOn == targetDate)
            .SumAsync(d => d.Calories);

        var burnedCal = await _context.ExerciseLogs
            .Where(e => e.UserId == userId && e.LoggedOn == targetDate)
            .SumAsync(e => e.CaloriesBurned);

        var remaining = GoalPolicy.RemainingCalories(dailyTarget, consumedCal, burnedCal);

        string status;
        string msg;
        if (remaining > 50)
        {
            status = "green";
            msg = $"Bạn còn dư {remaining:N0} kcal trong ngân sách calo hôm nay. Hãy duy trì hoặc bổ sung thêm bữa phụ lành mạnh.";
        }
        else if (remaining >= -50 && remaining <= 50)
        {
            status = "yellow";
            msg = $"Bạn đã nạp rất sát mức calo mục tiêu (còn {remaining:N0} kcal). Tuyệt vời, hãy giữ vững phong độ này!";
        }
        else
        {
            status = "red";
            msg = $"Bạn đã nạp vượt mức calo mục tiêu {Math.Abs(remaining):N0} kcal. Hãy chú ý giảm khẩu phần bữa tối hoặc tăng cường đi bộ/vận động nhẹ.";
        }

        return new RemainingCaloriesDto
        {
            Date = targetDate,
            DailyTarget = dailyTarget,
            Consumed = consumedCal,
            Burned = burnedCal,
            ExerciseCreditApplied = GoalPolicy.ExerciseCredit(burnedCal),
            Remaining = remaining,
            Status = status,
            Message = msg
        };
    }

    public async Task<string?> FindMentionedFoodAsync(string message)
    {
        var query = FoodText.SearchKey(message);
        var names = await _context.Foods.Where(f => !f.IsHidden).OrderBy(f => f.Id).Select(f => f.Name).ToListAsync();
        string? best = null;
        var bestLength = 0;
        foreach (var name in names)
        {
            var words = FoodText.Words(name);
            var prefix = words;
            while (prefix.Length > bestLength)
            {
                if (FoodText.ContainsWords(query, ChatSafetyFilter.RemoveDiacritics(prefix)))
                {
                    best = prefix == words ? name : prefix;
                    bestLength = prefix.Length;
                    break;
                }
                var lastSpace = prefix.LastIndexOf(' ');
                if (lastSpace < 0) break;
                prefix = prefix[..lastSpace];
                if (!prefix.Contains(' ')) break;
            }
        }
        return best;
    }

    private static FoodSearchResultDto Describe(Food f) => new()
    {
        Id = f.Id, Name = f.Name, Category = f.Category?.Name ?? "Món ăn",
        ServingSize = f.ServingSize, ServingUnit = f.ServingName, Calories = f.Calories,
        Protein = f.Protein, Carb = f.Carb, Fat = f.Fat, Fiber = f.Fiber, Sugar = f.Sugar, Sodium = f.Sodium,
        Ingredients = f.Ingredients, AllergenNotes = f.AllergenNotes, HealthWarning = f.HealthWarning,
        CookingState = FoodSeedData.CookingState(f.Name)
    };

    public async Task<List<FoodSearchResultDto>> SearchFoodsAsync(
        string? keyword,
        decimal? maxKcal = null,
        List<string>? excludeAllergens = null,
        bool vegetarianOnly = false,
        List<string>? dislikedFoods = null)
    {
        var query = _context.Foods
            .Include(f => f.Category)
            .Where(f => !f.IsHidden);

        if (maxKcal.HasValue && maxKcal.Value > 0)
        {
            query = query.Where(f => f.Calories <= maxKcal.Value);
        }

        if (vegetarianOnly)
        {
            query = query.Where(f => f.IsVegetarian);
        }

        var restrictions = new FoodRestrictions(excludeAllergens, dislikedFoods);
        var key = FoodText.SearchKey(keyword);
        var foods = await query.AsNoTracking().OrderBy(f => f.Id).ToListAsync();
        return foods.Where(restrictions.Allows)
            .Where(f => key.Length == 0 || FoodText.SearchKey(f.Name).Contains(key, StringComparison.Ordinal)
                || FoodText.SearchKey(f.Ingredients).Contains(key, StringComparison.Ordinal))
            .OrderByDescending(f => string.Equals(f.Name, keyword?.Trim(), StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => FoodText.SearchKey(f.Name) == key)
            .ThenByDescending(f => FoodText.ContainsWords(FoodText.SearchKey(f.Name), key))
            .ThenBy(f => f.Id)
            .Take(10).Select(Describe).ToList();
    }

    public async Task<MealSuggestionDto> SuggestMealAsync(
        decimal maxKcal,
        DietType dietType,
        List<string>? excludeAllergens = null,
        bool preferLowSodium = false,
        List<string>? dislikedFoods = null)
    {
        if (maxKcal <= 0) return new();

        var query = _context.Foods
            .Include(f => f.Category)
            .Where(f => !f.IsHidden);

        if (dietType == DietType.Vegetarian)
        {
            query = query.Where(f => f.IsVegetarian);
        }

        var restrictions = new FoodRestrictions(excludeAllergens, dislikedFoods);
        var foods = (await query.AsNoTracking().ToListAsync()).Where(restrictions.Allows).Where(FoodText.CanServe).ToList();

        var rnd = Random.Shared;
        IOrderedEnumerable<Food> Rank(IEnumerable<Food> candidates) =>
            candidates.OrderBy(food => preferLowSodium ? food.Sodium : 0m).ThenBy(_ => rnd.Next());

        Food Pick(IEnumerable<Food> candidates) => Rank(candidates).First();
        var mainDishes = foods.Where(f => FoodCategorySlugs.Matches(f.Category.Slug, FoodCategorySlugs.Main)).ToList();
        var carbs = foods.Where(f => FoodCategorySlugs.Matches(f.Category.Slug, FoodCategorySlugs.Starch)).ToList();
        var proteins = foods.Where(f => FoodCategorySlugs.Matches(f.Category.Slug, FoodCategorySlugs.Protein)).ToList();
        var veggies = foods.Where(f => FoodCategorySlugs.Matches(f.Category.Slug, FoodCategorySlugs.Vegetable)).ToList();

        var selected = new List<Food>();

        // Ưu tiên tổ hợp Tinh bột + Đạm + Rau củ
        if (carbs.Any() && proteins.Any() && veggies.Any())
        {
            var carb = Pick(carbs);
            var protein = Pick(proteins);
            var veg = Pick(veggies);

            if (carb.Calories + protein.Calories + veg.Calories <= maxKcal)
            {
                selected.Add(carb);
                selected.Add(protein);
                selected.Add(veg);
            }
        }

        // Nếu chưa chọn được, thử 1 món chính + 1 rau củ
        if (!selected.Any() && mainDishes.Any() && veggies.Any())
        {
            var veg = Pick(veggies);
            var main = Rank(mainDishes.Where(m => m.Calories + veg.Calories <= maxKcal)).FirstOrDefault();
            if (main != null)
            {
                selected.Add(main);
                selected.Add(veg);
            }
        }

        // Fallback chọn 1-2 món bất kỳ
        if (!selected.Any())
        {
            foreach (var food in Rank(foods))
            {
                if (food.Calories > maxKcal - selected.Sum(item => item.Calories)) continue;
                selected.Add(food);
                if (selected.Count == 2) break;
            }
        }

        var resultItems = selected.Select(Describe).ToList();

        var totalCal = resultItems.Sum(i => i.Calories);
        var totalP = resultItems.Sum(i => i.Protein);
        var totalC = resultItems.Sum(i => i.Carb);
        var totalF = resultItems.Sum(i => i.Fat);

        return new MealSuggestionDto
        {
            TargetMaxKcal = maxKcal,
            TotalKcal = totalCal,
            TotalProtein = totalP,
            TotalCarb = totalC,
            TotalFat = totalF,
            SuggestedItems = resultItems,
            Advice = $"Bữa ăn gợi ý gồm {resultItems.Count} món, cung cấp khoảng {totalCal:N0} kcal ({totalP:0.0}g Protein), phù hợp với ngân sách {maxKcal:N0} kcal của bạn."
        };
    }

    public async Task<SwapSuggestionDto> GetSwapSuggestionsAsync(
        string foodName,
        DietType dietType,
        List<string>? excludeAllergens = null,
        bool preferLowSodium = false,
        List<string>? dislikedFoods = null)
    {
        if (string.IsNullOrWhiteSpace(foodName))
        {
            return new SwapSuggestionDto { FoundOriginalFood = false, Note = "Vui lòng nhập tên món ăn cần thay thế." };
        }

        var key = FoodText.SearchKey(foodName);
        var catalogue = await _context.Foods.Include(f => f.Category).AsNoTracking()
            .Where(f => !f.IsHidden).OrderBy(f => f.Id).ToListAsync();
        var origFood = catalogue.FirstOrDefault(f => string.Equals(f.Name, foodName.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? catalogue.FirstOrDefault(f => FoodText.SearchKey(f.Name) == key)
            ?? catalogue.FirstOrDefault(f => FoodText.ContainsWords(FoodText.SearchKey(f.Name), key));

        if (origFood == null)
        {
            return new SwapSuggestionDto
            {
                OriginalFoodName = foodName,
                FoundOriginalFood = false,
                Note = $"Hiện chưa có dữ liệu chính xác của món '{foodName}' trong danh mục hệ thống."
            };
        }

        var restrictions = new FoodRestrictions(excludeAllergens, dislikedFoods);
        var originalIsReady = FoodText.CanServe(origFood);
        var candidates = catalogue.Where(f => f.CategoryId == origFood.CategoryId && f.Id != origFood.Id)
            .Where(f => dietType != DietType.Vegetarian || f.IsVegetarian).Where(restrictions.Allows)
            .Where(f => !originalIsReady || FoodText.CanServe(f));

        // Lọc các món có calo lệch trong khoảng ±15% hoặc ±60 kcal
        var matches = candidates
            .Where(c => Math.Abs(c.Calories - origFood.Calories) <= Math.Max(60m, origFood.Calories * 0.15m))
            .OrderBy(c => preferLowSodium ? c.Sodium : 0m)
            .ThenBy(c => Math.Abs(c.Calories - origFood.Calories))
            .Take(5)
            .Select(Describe).ToList();

        return new SwapSuggestionDto
        {
            OriginalFoodName = origFood.Name,
            FoundOriginalFood = true,
            OriginalCalories = origFood.Calories,
            OriginalProtein = origFood.Protein,
            AlternativeFoods = matches,
            Note = matches.Any()
                ? $"Tìm thấy {matches.Count} món thay thế cùng nhóm '{origFood.Category?.Name}' có mức calo và dinh dưỡng tương đương."
                : $"Chưa có món thay thế cùng nhóm có mức calo sát trong khoảng ±15%."
        };
    }
}
