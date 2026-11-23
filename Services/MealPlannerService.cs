using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;

namespace NutriTrackAI.Services;

public class MealPlannerService : IMealPlannerService
{
    private readonly AppDbContext _context;
    private readonly INutritionCalculatorService _nutritionCalculator;
    private readonly ILogger<MealPlannerService> _logger;
    private readonly CalorieBudgetService _budgets;

    public MealPlannerService(
        AppDbContext context,
        INutritionCalculatorService nutritionCalculator,
        ILogger<MealPlannerService> logger, CalorieBudgetService budgets)
    {
        _context = context;
        _nutritionCalculator = nutritionCalculator;
        _logger = logger;
        _budgets = budgets;
    }

    public async Task<MealPlanResult> GeneratePlanAsync(int userId, int? customTargetCalories = null, int numberOfMeals = 4)
    {
        // 1. Lấy thông tin hồ sơ UserProfile & Mục tiêu Goal
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            throw new InvalidOperationException("Hồ sơ người dùng chưa được khởi tạo. Vui lòng hoàn tất Onboarding.");
        }

        // Active plan is authoritative; custom client calories cannot override it.
        var budget = await _budgets.GetAsync(userId);
        if (budget.Suspended)
            throw new InvalidOperationException("Chưa thể sinh thực đơn theo ngân sách tự động. Bạn vẫn có thể tra cứu thực phẩm và ghi nhật ký.");
        var targetCalories = budget.TargetCalories;
        var goalType = budget.Goal?.GoalType ?? GoalType.Maintain;
        var constraints = budget.Health.Constraints;
        var macros = _budgets.GetMacros(profile, budget);

        numberOfMeals = Math.Max(numberOfMeals == 3 ? 3 : 4, constraints.MinimumMeals ?? 0);

        // Bước 1: Phân bổ ngân sách calo theo bữa (Mục 4.6 GEMINI.md)
        // 4 bữa: Sáng 25%, Trưa 35%, Tối 30%, Phụ 10%
        // 3 bữa: Sáng 30%, Trưa 40%, Tối 30%
        int bCal, lCal, dCal, sCal;
        if (numberOfMeals == 4)
        {
            bCal = (int)Math.Round(targetCalories * 0.25m);
            lCal = (int)Math.Round(targetCalories * 0.35m);
            dCal = (int)Math.Round(targetCalories * 0.30m);
            sCal = targetCalories - (bCal + lCal + dCal); // 10%
        }
        else
        {
            bCal = (int)Math.Round(targetCalories * 0.30m);
            lCal = (int)Math.Round(targetCalories * 0.40m);
            dCal = targetCalories - (bCal + lCal); // 30%
            sCal = 0;
        }

        // Bước 2: Bộ lọc cứng an toàn (Mục 4.6 GEMINI.md)
        // - Loại bỏ món IsHidden == true
        // - Lọc món chay nếu DietType == Vegetarian
        // - Loại bỏ món chứa Allergies của User
        // - Loại bỏ món trùng DislikedFoods của User
        var allFoods = await _context.Foods
            .Include(f => f.Category)
            .Where(f => !f.IsHidden)
            .ToListAsync();

        if (profile.DietType == DietType.Vegetarian)
        {
            allFoods = allFoods.Where(f => f.IsVegetarian).ToList();
        }

        var userAllergies = FoodRestrictions.Split(profile.Allergies);
        var userDislikes = FoodRestrictions.Split(profile.DislikedFoods);
        var restrictions = new FoodRestrictions(userAllergies, userDislikes);
        var safeFoods = allFoods.Where(restrictions.Allows).Where(FoodText.CanServe).ToList();

        if (!safeFoods.Any())
        {
            throw new InvalidOperationException("Không tìm thấy món ăn nào thỏa mãn các điều kiện dị ứng và sở thích của bạn.");
        }

        // Phân loại nhóm món an toàn
        var mainDishes = safeFoods.Where(f => FoodCategorySlugs.Matches(f.Category.Slug, FoodCategorySlugs.Main)).ToList(); // Món chính
        var carbs = safeFoods.Where(f => FoodCategorySlugs.Matches(f.Category.Slug, FoodCategorySlugs.Starch)).ToList();      // Tinh bột & ngũ cốc
        var proteins = safeFoods.Where(f => FoodCategorySlugs.Matches(f.Category.Slug, FoodCategorySlugs.Protein)).ToList();   // Thịt, cá, đạm
        var veggies = safeFoods.Where(f => FoodCategorySlugs.Matches(f.Category.Slug, FoodCategorySlugs.Vegetable)).ToList();    // Rau củ & nấm
        var fruits = safeFoods.Where(f => FoodCategorySlugs.Matches(f.Category.Slug, FoodCategorySlugs.Fruit)).ToList();     // Trái cây
        var snacks = safeFoods.Where(f => FoodCategorySlugs.Matches(f.Category.Slug, FoodCategorySlugs.Drink)).ToList();     // Đồ uống & ăn vặt

        var rnd = Random.Shared;
        IOrderedEnumerable<Food> Rank(IEnumerable<Food> candidates) =>
            candidates.OrderBy(food => constraints.MaxSodiumMg.HasValue ? food.Sodium : 0m).ThenBy(_ => rnd.Next());

        Food Pick(IReadOnlyList<Food> candidates) =>
            constraints.MaxSodiumMg.HasValue ? Rank(candidates).First() : candidates[rnd.Next(candidates.Count)];

        // Bước 3: Chọn tổ hợp món cho từng bữa
        var meals = new List<MealPlanMealDto>();

        // 3.1 Bữa sáng (~25% hoặc 30%)
        var breakfastItems = new List<Food>();
        var morningMains = mainDishes.Where(m => m.Name.Contains("Phở") || m.Name.Contains("Bún") || m.Name.Contains("Bánh mì") || m.Name.Contains("Cháo")).ToList();
        if (morningMains.Any())
        {
            breakfastItems.Add(Pick(morningMains));
        }
        else if (carbs.Any() && proteins.Any())
        {
            breakfastItems.Add(Pick(carbs));
            breakfastItems.Add(Pick(proteins));
        }
        else
        {
            breakfastItems.Add(Pick(safeFoods));
        }

        // 3.2 Bữa trưa (~35% hoặc 40%)
        var lunchItems = new List<Food>();
        if (carbs.Any() && proteins.Any())
        {
            lunchItems.Add(Pick(carbs));
            lunchItems.Add(Pick(proteins));
            if (veggies.Any())
            {
                lunchItems.Add(Pick(veggies));
            }
        }
        else if (mainDishes.Any())
        {
            lunchItems.Add(Pick(mainDishes));
            if (veggies.Any())
            {
                lunchItems.Add(Pick(veggies));
            }
        }
        else
        {
            lunchItems.AddRange(Rank(safeFoods).Take(2));
        }

        // 3.3 Bữa tối (~30%)
        var dinnerItems = new List<Food>();
        Food PickForDinner(List<Food> candidates)
        {
            if (constraints.MaxSodiumMg.HasValue)
                return candidates.OrderBy(food => food.Sodium).ThenBy(lunchItems.Contains).ThenBy(_ => rnd.Next()).First();
            var alternatives = candidates.Where(food => !lunchItems.Contains(food)).ToList();
            return Pick(alternatives.Count > 0 ? alternatives : candidates);
        }

        if (carbs.Any() && proteins.Any())
        {
            dinnerItems.Add(PickForDinner(carbs));
            dinnerItems.Add(PickForDinner(proteins));
            if (veggies.Any()) dinnerItems.Add(PickForDinner(veggies));
        }
        else if (mainDishes.Any())
        {
            dinnerItems.Add(PickForDinner(mainDishes));
            if (veggies.Any()) dinnerItems.Add(PickForDinner(veggies));
        }
        else
        {
            dinnerItems.AddRange(Rank(safeFoods).Take(2));
        }

        // 3.4 Bữa phụ (Nếu 4 bữa, ~10%)
        var snackItems = new List<Food>();
        if (numberOfMeals == 4)
        {
            if (fruits.Any())
            {
                snackItems.Add(Pick(fruits));
            }
            else if (snacks.Any())
            {
                snackItems.Add(Pick(snacks));
            }
            else
            {
                snackItems.Add(Pick(safeFoods));
            }
        }

        // Bước 3b: Căn chỉnh khẩu phần (Servings Scaling) để tổng calo đạt trong khoảng ±10%
        var unscaledTotalCalories = breakfastItems.Sum(x => x.Calories)
                                  + lunchItems.Sum(x => x.Calories)
                                  + dinnerItems.Sum(x => x.Calories)
                                  + snackItems.Sum(x => x.Calories);

        decimal scaleRatio = unscaledTotalCalories > 0 ? ((decimal)targetCalories / unscaledTotalCalories) : 1m;
        // Giới hạn tỷ lệ điều chỉnh trong khoảng an toàn 0.75 - 1.35
        scaleRatio = Math.Clamp(scaleRatio, 0.75m, 1.35m);

        decimal? carbPerMeal = constraints.DistributeCarbs && macros is { UnavailableReason: null }
            ? macros.CarbGrams / numberOfMeals : null;
        decimal PortionScale(List<Food> candidates)
        {
            var carb = candidates.Sum(food => food.Carb);
            return carbPerMeal.HasValue && carb > 0 ? Math.Clamp(carbPerMeal.Value / carb, 0.75m, 1.35m) : scaleRatio;
        }

        // Tạo MealPlanMealDto cho từng bữa
        var breakfastDto = BuildMealDto(MealType.Breakfast, "Bữa sáng", "bi-sunrise-fill text-warning", bCal, breakfastItems, PortionScale(breakfastItems));
        var lunchDto = BuildMealDto(MealType.Lunch, "Bữa trưa", "bi-sun-fill text-primary", lCal, lunchItems, PortionScale(lunchItems));
        var dinnerDto = BuildMealDto(MealType.Dinner, "Bữa tối", "bi-moon-stars-fill text-indigo", dCal, dinnerItems, PortionScale(dinnerItems));

        meals.Add(breakfastDto);
        meals.Add(lunchDto);
        meals.Add(dinnerDto);

        if (numberOfMeals == 4 && snackItems.Any())
        {
            var snackDto = BuildMealDto(MealType.Snack, "Bữa phụ / Ăn vặt", "bi-cup-hot-fill text-success", sCal, snackItems, PortionScale(snackItems));
            meals.Add(snackDto);
        }

        // Tính tổng dinh dưỡng toàn thực đơn
        var allPlanItems = meals.SelectMany(m => m.Items).ToList();
        var totalCal = allPlanItems.Sum(i => i.Calories);
        var totalP = allPlanItems.Sum(i => i.Protein);
        var totalC = allPlanItems.Sum(i => i.Carb);
        var totalF = allPlanItems.Sum(i => i.Fat);
        var totalFiber = allPlanItems.Sum(i => i.Fiber);
        var totalSugar = allPlanItems.Sum(i => i.Sugar);
        var totalSodium = allPlanItems.Sum(i => i.Sodium);

        // Tính tỷ lệ % năng lượng từ P/C/F
        var totalEnergy = (totalP * 4) + (totalC * 4) + (totalF * 9);
        var pPercent = totalEnergy > 0 ? (int)Math.Round((totalP * 4 * 100) / totalEnergy) : 20;
        var cPercent = totalEnergy > 0 ? (int)Math.Round((totalC * 4 * 100) / totalEnergy) : 50;
        var fPercent = totalEnergy > 0 ? (int)Math.Round((totalF * 9 * 100) / totalEnergy) : 30;

        var calDiff = targetCalories > 0 ? Math.Round(((totalCal - targetCalories) / targetCalories) * 100m, 1) : 0;
        var isWithinTarget = Math.Abs(calDiff) <= 10m;

        // Bước 4: Tự động sinh lời giải thích (Explanation)
        var allergiesStr = userAllergies.Any() ? string.Join(", ", userAllergies) : "Không có";
        var dislikesStr = userDislikes.Any() ? string.Join(", ", userDislikes) : "Không có";
        var goalName = GetGoalTypeName(goalType);

        var explanation = $"Thực đơn cá nhân hóa được thiết kế tối ưu cho mục tiêu {goalName}, " +
                          $"ngân sách {targetCalories:N0} kcal/ngày (thực tế {totalCal:N0} kcal, chênh lệch {calDiff:+0.0;-0.0;0}%). " +
                          $"Tỷ lệ theo món đã chọn: {pPercent}% chất đạm ({totalP:0.0} g), {cPercent}% tinh bột ({totalC:0.0} g), {fPercent}% chất béo ({totalF:0.0} g). " +
                          $"Hệ thống đã đối chiếu dữ liệu thành phần với dị ứng đã khai báo ({allergiesStr}) " +
                          $"và các món ăn không thích ({dislikesStr}) của bạn.";

        // Danh sách nguyên liệu tổng hợp
        var aggregatedIngredients = allPlanItems
            .Where(i => !string.IsNullOrWhiteSpace(i.Ingredients))
            .SelectMany(i => i.Ingredients!.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        return new MealPlanResult
        {
            Name = $"Thực đơn {goalName} ({numberOfMeals} bữa) - {totalCal:N0} kcal",
            GoalType = goalType,
            GoalTypeName = goalName,
            TargetCalories = targetCalories,
            TotalCalories = totalCal,
            TotalProtein = totalP,
            TotalCarb = totalC,
            TotalFat = totalF,
            TotalFiber = totalFiber,
            TotalSugar = totalSugar,
            TotalSodium = totalSodium,
            ProteinPercent = pPercent,
            CarbPercent = cPercent,
            FatPercent = fPercent,
            CalorieDifferencePercent = calDiff,
            IsWithinTarget = isWithinTarget,
            Explanation = explanation,
            RecommendedMacros = macros,
            Meals = meals,
            AggregatedIngredients = aggregatedIngredients
        };
    }

    public async Task<List<FoodSwapCandidate>> GetSwapCandidatesAsync(int foodId, decimal currentServings, int userId)
    {
        var currentFood = await _context.Foods
            .Include(f => f.Category)
            .FirstOrDefaultAsync(f => f.Id == foodId);

        if (currentFood == null) return new List<FoodSwapCandidate>();

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        var restrictions = new FoodRestrictions(FoodRestrictions.Split(profile?.Allergies), FoodRestrictions.Split(profile?.DislikedFoods));

        // Mức calo của món hiện tại với khẩu phần đang chọn
        var ratio = currentFood.ServingSize > 0 ? (currentServings / currentFood.ServingSize) : 1m;
        var currentItemCalories = currentFood.Calories * ratio;

        // Tìm các món cùng CategoryId, chưa bị ẩn
        var candidatesQuery = _context.Foods
            .Include(f => f.Category)
            .Where(f => f.CategoryId == currentFood.CategoryId && f.Id != currentFood.Id && !f.IsHidden);

        if (profile?.DietType == DietType.Vegetarian)
        {
            candidatesQuery = candidatesQuery.Where(f => f.IsVegetarian);
        }

        var candidateFoods = await candidatesQuery.ToListAsync();

        // Lọc dị ứng và món không thích
        var safeCandidates = candidateFoods.Where(restrictions.Allows).Where(FoodText.CanServe).ToList();

        // Lọc các món có calo lệch trong khoảng ±15% (hoặc ±60 kcal)
        var result = new List<FoodSwapCandidate>();
        foreach (var c in safeCandidates)
        {
            var diffKcal = c.Calories - currentFood.Calories;
            var diffPercent = currentFood.Calories > 0 ? (diffKcal / currentFood.Calories) * 100m : 0;

            if (Math.Abs(diffPercent) <= 15m || Math.Abs(diffKcal) <= 60m)
            {
                result.Add(new FoodSwapCandidate
                {
                    FoodId = c.Id,
                    FoodName = c.Name,
                    ImageUrl = c.ImageUrl,
                    CategoryId = c.CategoryId,
                    CategoryName = c.Category?.Name ?? "Món ăn",
                    ServingSize = c.ServingSize,
                    ServingUnit = c.ServingName,
                    Calories = c.Calories,
                    Protein = c.Protein,
                    Carb = c.Carb,
                    Fat = c.Fat,
                    Fiber = c.Fiber,
                    Sugar = c.Sugar,
                    Ingredients = c.Ingredients,
                    AllergenNotes = c.AllergenNotes,
                    Sodium = c.Sodium,
                    CalorieDiffKcal = Math.Round(diffKcal, 1),
                    CalorieDiffPercent = Math.Round(diffPercent, 1)
                });
            }
        }

        var preferLowSodium = profile != null && GoalPolicy.EvaluateHealth(profile).Constraints.MaxSodiumMg.HasValue;
        return result.OrderBy(x => preferLowSodium ? x.Sodium : 0m)
            .ThenBy(x => Math.Abs(x.CalorieDiffPercent)).Take(10).ToList();
    }

    public async Task<int> SavePlanAsync(int userId, string planName, MealPlanResult planResult)
    {
        var plan = new MealPlan
        {
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(planName) ? planResult.Name : planName,
            GoalType = planResult.GoalType,
            TargetCalories = planResult.TargetCalories,
            IsTemplate = false,
            IsPublic = false,
            Explanation = planResult.Explanation,
            CreatedAt = DateTime.UtcNow
        };

        _context.MealPlans.Add(plan);
        await _context.SaveChangesAsync();

        int order = 1;
        foreach (var meal in planResult.Meals)
        {
            foreach (var item in meal.Items)
            {
                var planItem = new MealPlanItem
                {
                    MealPlanId = plan.Id,
                    MealType = meal.MealType,
                    FoodId = item.FoodId,
                    Servings = item.Servings,
                    SortOrder = order++
                };
                _context.MealPlanItems.Add(planItem);
            }
        }

        await _context.SaveChangesAsync();
        return plan.Id;
    }

    public async Task<bool> ApplyPlanToTodayAsync(int userId, MealPlanResult planResult, DateTime? targetDate = null)
    {
        var logDate = (targetDate ?? GoalPolicy.Today).Date;

        foreach (var meal in planResult.Meals)
        {
            // Tìm hoặc tạo MealLog
            var mealLog = await _context.MealLogs
                .FirstOrDefaultAsync(m => m.UserId == userId && m.LoggedOn == logDate && m.MealType == meal.MealType);

            if (mealLog == null)
            {
                mealLog = new MealLog
                {
                    UserId = userId,
                    LoggedOn = logDate,
                    MealType = meal.MealType,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MealLogs.Add(mealLog);
                await _context.SaveChangesAsync();
            }

            // Thêm các món trong bữa vào MealLogDetails
            foreach (var item in meal.Items)
            {
                var detail = new MealLogDetail
                {
                    MealLogId = mealLog.Id,
                    FoodId = item.FoodId,
                    Servings = item.Servings,
                    Calories = item.Calories,
                    Protein = item.Protein,
                    Carb = item.Carb,
                    Fat = item.Fat,
                    Fiber = item.Fiber,
                    Sugar = item.Sugar,
                    Sodium = item.Sodium
                };
                _context.MealLogDetails.Add(detail);
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public Task<List<MealPlanTemplateSummaryDto>> GetPublicTemplatesAsync() =>
        GetPlanSummariesAsync(_context.MealPlans.Where(p => p.IsTemplate && p.IsPublic));

    public Task<List<MealPlanTemplateSummaryDto>> GetUserSavedPlansAsync(int userId) =>
        GetPlanSummariesAsync(_context.MealPlans.Where(p => p.UserId == userId));

    private static Task<List<MealPlanTemplateSummaryDto>> GetPlanSummariesAsync(IQueryable<MealPlan> plans) =>
        plans.AsNoTracking().OrderByDescending(t => t.CreatedAt).Select(t => new MealPlanTemplateSummaryDto
        {
            Id = t.Id,
            Name = t.Name,
            GoalType = t.GoalType,
            GoalTypeName = GetGoalTypeName(t.GoalType),
            TargetCalories = t.TargetCalories,
            IsTemplate = t.IsTemplate,
            IsPublic = t.IsPublic,
            Explanation = t.Explanation,
            TotalFoodsCount = t.Items.Count,
            PreviewFood = t.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new Food { Name = i.Food.Name, ImageUrl = i.Food.ImageUrl })
                .FirstOrDefault(),
            CreatedAt = t.CreatedAt
        }).ToListAsync();

    public async Task<MealPlanResult?> GetPlanByIdAsync(int planId, int userId)
    {
        var plan = await _context.MealPlans
            .Include(p => p.Items)
                .ThenInclude(i => i.Food)
                    .ThenInclude(f => f.Category)
            .FirstOrDefaultAsync(p => p.Id == planId && (p.UserId == userId || (p.IsTemplate && p.IsPublic)));

        if (plan == null) return null;

        var meals = new List<MealPlanMealDto>();
        var grouped = plan.Items.GroupBy(i => i.MealType).OrderBy(g => (int)g.Key);

        foreach (var group in grouped)
        {
            var mealType = group.Key;
            var mealName = GetMealTypeName(mealType);
            var mealIcon = GetMealIcon(mealType);

            var mealDto = new MealPlanMealDto
            {
                MealType = mealType,
                MealName = mealName,
                MealIcon = mealIcon
            };

            foreach (var item in group.OrderBy(x => x.SortOrder))
            {
                var f = item.Food;
                var ratio = f.ServingSize > 0 ? (item.Servings / f.ServingSize) : 1m;

                mealDto.Items.Add(new MealPlanItemDto
                {
                    FoodId = f.Id,
                    FoodName = f.Name,
                    CategoryId = f.CategoryId,
                    CategoryName = f.Category?.Name ?? "Món ăn",
                    ImageUrl = f.ImageUrl,
                    Servings = item.Servings,
                    ServingUnit = f.ServingName,
                    StandardServingSize = f.ServingSize,
                    Calories = Math.Round(f.Calories * ratio, 1),
                    Protein = Math.Round(f.Protein * ratio, 1),
                    Carb = Math.Round(f.Carb * ratio, 1),
                    Fat = Math.Round(f.Fat * ratio, 1),
                    Fiber = Math.Round(f.Fiber * ratio, 1),
                    Sugar = Math.Round(f.Sugar * ratio, 1),
                    Sodium = Math.Round(f.Sodium * ratio, 1),
                    AllergenNotes = f.AllergenNotes,
                    Ingredients = f.Ingredients
                });
            }

            mealDto.TotalCalories = mealDto.Items.Sum(x => x.Calories);
            mealDto.TotalProtein = mealDto.Items.Sum(x => x.Protein);
            mealDto.TotalCarb = mealDto.Items.Sum(x => x.Carb);
            mealDto.TotalFat = mealDto.Items.Sum(x => x.Fat);
            meals.Add(mealDto);
        }

        var allItems = meals.SelectMany(m => m.Items).ToList();
        var totalCal = allItems.Sum(x => x.Calories);
        var totalP = allItems.Sum(x => x.Protein);
        var totalC = allItems.Sum(x => x.Carb);
        var totalF = allItems.Sum(x => x.Fat);

        var totalEnergy = (totalP * 4) + (totalC * 4) + (totalF * 9);
        var pPercent = totalEnergy > 0 ? (int)Math.Round((totalP * 4 * 100) / totalEnergy) : 20;
        var cPercent = totalEnergy > 0 ? (int)Math.Round((totalC * 4 * 100) / totalEnergy) : 50;
        var fPercent = totalEnergy > 0 ? (int)Math.Round((totalF * 9 * 100) / totalEnergy) : 30;

        var calDiff = plan.TargetCalories > 0 ? Math.Round(((totalCal - plan.TargetCalories) / plan.TargetCalories) * 100m, 1) : 0;

        var aggregatedIngredients = allItems
            .Where(i => !string.IsNullOrWhiteSpace(i.Ingredients))
            .SelectMany(i => i.Ingredients!.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        return new MealPlanResult
        {
            MealPlanId = plan.Id,
            Name = plan.Name,
            GoalType = plan.GoalType,
            GoalTypeName = GetGoalTypeName(plan.GoalType),
            TargetCalories = plan.TargetCalories,
            TotalCalories = totalCal,
            TotalProtein = totalP,
            TotalCarb = totalC,
            TotalFat = totalF,
            TotalFiber = allItems.Sum(x => x.Fiber),
            TotalSugar = allItems.Sum(x => x.Sugar),
            TotalSodium = allItems.Sum(x => x.Sodium),
            ProteinPercent = pPercent,
            CarbPercent = cPercent,
            FatPercent = fPercent,
            CalorieDifferencePercent = calDiff,
            IsWithinTarget = Math.Abs(calDiff) <= 10m,
            Explanation = plan.Explanation ?? string.Empty,
            Meals = meals,
            AggregatedIngredients = aggregatedIngredients
        };
    }

    private MealPlanMealDto BuildMealDto(
        MealType mealType,
        string mealName,
        string icon,
        int targetCalories,
        List<Food> foods,
        decimal scaleRatio)
    {
        var mealDto = new MealPlanMealDto
        {
            MealType = mealType,
            MealName = mealName,
            MealIcon = icon,
            TargetCalories = targetCalories
        };

        foreach (var food in foods)
        {
            // Khẩu phần được tinh chỉnh theo hệ số
            decimal scaledServings;
            if (food.ServingSize >= 50)
            {
                // Làm tròn đến 5g hoặc 10g gần nhất
                var rawServings = food.ServingSize * scaleRatio;
                scaledServings = Math.Max(20m, Math.Round(rawServings / 5m) * 5m);
            }
            else
            {
                // Món tính theo quả, đĩa, phần: làm tròn 0.5
                var rawServings = food.ServingSize * scaleRatio;
                scaledServings = Math.Max(0.5m, Math.Round(rawServings * 2m) / 2m);
            }

            var ratio = food.ServingSize > 0 ? (scaledServings / food.ServingSize) : 1m;

            mealDto.Items.Add(new MealPlanItemDto
            {
                FoodId = food.Id,
                FoodName = food.Name,
                CategoryId = food.CategoryId,
                CategoryName = food.Category?.Name ?? "Món ăn",
                ImageUrl = food.ImageUrl,
                Servings = scaledServings,
                ServingUnit = food.ServingName,
                StandardServingSize = food.ServingSize,
                Calories = Math.Round(food.Calories * ratio, 1),
                Protein = Math.Round(food.Protein * ratio, 1),
                Carb = Math.Round(food.Carb * ratio, 1),
                Fat = Math.Round(food.Fat * ratio, 1),
                Fiber = Math.Round(food.Fiber * ratio, 1),
                Sugar = Math.Round(food.Sugar * ratio, 1),
                Sodium = Math.Round(food.Sodium * ratio, 1),
                AllergenNotes = food.AllergenNotes,
                Ingredients = food.Ingredients
            });
        }

        mealDto.TotalCalories = mealDto.Items.Sum(x => x.Calories);
        mealDto.TotalProtein = mealDto.Items.Sum(x => x.Protein);
        mealDto.TotalCarb = mealDto.Items.Sum(x => x.Carb);
        mealDto.TotalFat = mealDto.Items.Sum(x => x.Fat);

        return mealDto;
    }

    private static string GetGoalTypeName(GoalType goalType) => goalType switch
    {
        GoalType.Lose => "Giảm cân",
        GoalType.Gain => "Tăng cân",
        GoalType.Maintain => "Duy trì cân nặng",
        _ => "Duy trì cân nặng"
    };

    private string GetMealTypeName(MealType type) => type switch
    {
        MealType.Breakfast => "Bữa sáng",
        MealType.Lunch => "Bữa trưa",
        MealType.Dinner => "Bữa tối",
        MealType.Snack => "Bữa phụ / Ăn vặt",
        _ => "Bữa ăn"
    };

    private string GetMealIcon(MealType type) => type switch
    {
        MealType.Breakfast => "bi-sunrise-fill text-warning",
        MealType.Lunch => "bi-sun-fill text-primary",
        MealType.Dinner => "bi-moon-stars-fill text-indigo",
        MealType.Snack => "bi-cup-hot-fill text-success",
        _ => "bi-egg-fried"
    };
}
