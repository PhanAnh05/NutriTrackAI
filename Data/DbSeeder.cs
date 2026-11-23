using NutriTrackAI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<AppDbContext>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        // 1. Seed Roles
        await SeedRolesAsync(roleManager);

        // 2. Seed Users & Profile
        await SeedUsersAsync(userManager, context);

        await FoodSeedData.UpsertAsync(context);

        // 5. Seed Exercises (10 hoạt động vận động)
        await SeedExercisesAsync(context);

        // 6. Seed Articles & FAQs & Medical Disclaimer (12+ bản ghi)
        await SeedArticlesAsync(context);

        // 7. Seed Meal Plan Templates (Thực đơn mẫu chuẩn chuyên gia)
        await SeedMealPlansAsync(context);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<int>> roleManager)
    {
        string[] roles = ["Admin", "User"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager, AppDbContext context)
    {
        // 1. Admin Account
        var adminEmail = "admin@nutritrack.vn";
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Quản trị viên Hệ thống",
                RoleName = "Admin",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // 2. Sample User (Đã hoàn tất Onboarding)
        var userEmail = "user@nutritrack.vn";
        var existingUser = await userManager.FindByEmailAsync(userEmail);
        if (existingUser == null)
        {
            var sampleUser = new ApplicationUser
            {
                UserName = userEmail,
                Email = userEmail,
                FullName = "Nguyễn Văn Mẫu",
                RoleName = "User",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(sampleUser, "User123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(sampleUser, "User");

                // Seed Profile
                var profile = new UserProfile
                {
                    UserId = sampleUser.Id,
                    Gender = Gender.Male,
                    DateOfBirth = new DateTime(2002, 5, 15),
                    HeightCm = 170m,
                    CurrentWeightKg = 72m,
                    TargetWeightKg = 65m,
                    ActivityLevel = ActivityLevel.Moderate,
                    DietType = DietType.Normal,
                    Allergies = "tôm, đậu phộng",
                    DislikedFoods = "hành tây",
                    HealthNotes = "Sức khỏe bình thường, muốn giảm mỡ bụng an toàn.",
                    DailyWaterGoalMl = 2000,
                    OnboardingCompleted = true,
                    UpdatedAt = DateTime.UtcNow
                };
                context.UserProfiles.Add(profile);

                // Seed Mục tiêu cân nặng (Goal)
                var goal = new Goal
                {
                    UserId = sampleUser.Id,
                    GoalType = GoalType.Lose,
                    StartWeight = 72m,
                    TargetWeight = 65m,
                    StartDate = GoalPolicy.Today,
                    TargetDate = GoalPolicy.Today.AddDays(70),
                    WeeklyRateKg = 0.7m,
                    DailyCalorieTarget = 1850,
                    IsActive = true,
                    WarningFlag = false,
                    WarningMessage = null,
                    CreatedAt = DateTime.UtcNow
                };
                context.Goals.Add(goal);

                // Seed Nhật ký cân nặng ban đầu (WeightLog)
                var weightLog = new WeightLog
                {
                    UserId = sampleUser.Id,
                    LoggedOn = GoalPolicy.Today,
                    WeightKg = 72m,
                    Note = "Bắt đầu hành trình giảm cân cùng NutriTrackAI",
                    CreatedAt = DateTime.UtcNow
                };
                context.WeightLogs.Add(weightLog);

                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task SeedExercisesAsync(AppDbContext context)
    {
        if (await context.Exercises.AnyAsync()) return;

        var exercises = new List<Exercise>
        {
            new() { Name = "Đi bộ nhẹ", MetValue = 3.0m, IsActive = true },
            new() { Name = "Đi bộ nhanh", MetValue = 4.3m, IsActive = true },
            new() { Name = "Chạy bộ 8km/h", MetValue = 8.0m, IsActive = true },
            new() { Name = "Đạp xe trung bình", MetValue = 6.8m, IsActive = true },
            new() { Name = "Tập Gym / Kháng lực", MetValue = 5.0m, IsActive = true },
            new() { Name = "Bơi lội", MetValue = 7.0m, IsActive = true },
            new() { Name = "Nhảy dây", MetValue = 10.0m, IsActive = true },
            new() { Name = "Yoga", MetValue = 2.5m, IsActive = true },
            new() { Name = "Cầu lông", MetValue = 5.5m, IsActive = true },
            new() { Name = "Bóng đá", MetValue = 7.0m, IsActive = true }
        };

        context.Exercises.AddRange(exercises);
        await context.SaveChangesAsync();
    }

    private static async Task SeedArticlesAsync(AppDbContext context)
    {
        if (await context.Articles.AnyAsync()) return;

        var articles = new List<Article>
        {
            new()
            {
                Type = ArticleType.Disclaimer,
                Title = "Cảnh báo Y tế và Miễn trừ Trách nhiệm Pháp lý",
                Body = "Thông tin trên website và nội dung do trợ lý AI tạo ra chỉ nhằm hỗ trợ theo dõi dinh dưỡng, cân nặng và lối sống, mang tính tham khảo. Hệ thống không phải thiết bị y tế, không chẩn đoán, không kê đơn và không thay thế ý kiến của bác sĩ, chuyên gia dinh dưỡng hoặc cơ sở y tế. Nếu bạn có bệnh lý, đang dùng thuốc, mang thai, hoặc có triệu chứng bất thường, hãy tham khảo chuyên môn trước khi thay đổi chế độ ăn.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Type = ArticleType.Faq,
                Title = "Nguyên tắc thâm hụt calo (Calorie Deficit) là gì?",
                Body = "Thâm hụt calo là trạng thái khi lượng calo bạn tiêu hao trong ngày (TDEE) lớn hơn lượng calo bạn nạp vào qua thức ăn và đồ uống. Để giảm 1 kg mỡ thừa, cơ thể cần thâm hụt khoảng 7.700 kcal. Mức thâm hụt an toàn và bền vững được các chuyên gia khuyến nghị là từ 300 đến 500 kcal mỗi ngày, tương đương giảm khoảng 0.5 kg mỗi tuần mà không làm suy giảm cơ bắp hay trao đổi chất.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Type = ArticleType.Faq,
                Title = "Người đang giảm cân có nên uống trà sữa không?",
                Body = "Một ly trà sữa trân châu thông thường (size L) có thể chứa từ 400 đến 550 kcal cùng với 50-65g đường tinh luyện (tương đương 10-13 thìa đường). Lượng calo này tương đương một bữa ăn chính nhưng hầu như không có protein hay chất xơ (calo rỗng). Nếu đang trong giai đoạn giảm mỡ, bạn nên hạn chế tối đa hoặc chỉ uống size nhỏ, chọn mức 0% hoặc 30% đường, không topping trân châu và tính toán lượng calo này vào ngân sách calo trong ngày.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Type = ArticleType.Faq,
                Title = "Cách thay thế thịt gà bằng nguồn đạm khác khi bị ngán?",
                Body = "Khi ngán ức gà, bạn có thể linh hoạt thay thế bằng nhiều nguồn đạm có giá trị sinh học cao tương đương:\n- Thủy hải sản: Tôm hấp (31g protein/150g), cá hồi (33g protein/150g), cá thu, cá basa.\n- Thịt đỏ nạc: Thăn bò (31g protein/120g), thịt nạc thăn heo luộc (32g protein/120g).\n- Trứng và sữa: Trứng gà (6g protein/quả), sữa chua Hy Lạp (12g protein/hũ).\n- Nguồn thực vật: Đậu hũ trắng, đậu nành, hạt hạnh nhân.\nHãy đa dạng hóa nguồn đạm để đảm bảo đầy đủ axit amin thiết yếu.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Type = ArticleType.Faq,
                Title = "Lượng protein cần thiết mỗi ngày cho người tập luyện và duy trì sức khỏe?",
                Body = "Nhu cầu protein phụ thuộc vào mức độ vận động:\n- Người ít vận động: 0.8 - 1.0g protein/kg cân nặng.\n- Người tập thể dục vừa phải: 1.2 - 1.6g protein/kg cân nặng.\n- Người tập kháng lực, tăng cơ hoặc đang siết cân: 1.6 - 2.2g protein/kg cân nặng.\nVí dụ một người nặng 70kg đang tập gym giảm mỡ cần khoảng 110 - 140g protein/ngày, chia đều vào 3-4 bữa để tối ưu quá trình tổng hợp protein cơ bắp.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Type = ArticleType.Faq,
                Title = "Tốc độ giảm cân an toàn là bao nhiêu mỗi tuần?",
                Body = "Tốc độ giảm cân bền vững và an toàn cho sức khỏe là từ 0.5 đến 1.0 kg/tuần (tương đương thâm hụt 500 - 750 kcal/ngày). Giảm cân quá nhanh (> 1kg/tuần) dễ dẫn đến mất khối lượng cơ nạc, sụt giảm trao đổi chất cơ bản, rụng tóc, mệt mỏi, sỏi mật và nguy cơ tăng cân trở lại (hiệu ứng yoyo). Hệ thống NutriTrackAI sẽ tự động bật cảnh báo đỏ nếu bạn thiết lập mục tiêu giảm cân vượt ngưỡng an toàn này.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Type = ArticleType.Faq,
                Title = "Mỗi ngày nên uống bao nhiêu nước để tối ưu trao đổi chất?",
                Body = "Lượng nước khuyến nghị cơ bản cho người trưởng thành là từ 35 - 40ml trên mỗi kg trọng lượng cơ thể (trung bình 2.000 - 2.500ml/ngày). Nếu bạn tập luyện thể thao ra nhiều mồ hôi hoặc thời tiết nắng nóng, cần bù thêm 500 - 1.000ml nước. Uống đủ nước giúp thanh lọc độc tố, giảm cảm giác đói giả tạo và hỗ trợ đốt cháy calo hiệu quả hơn.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Type = ArticleType.Faq,
                Title = "Chỉ số BMR và TDEE là gì? Cách tính và ứng dụng trong quản lý cân nặng?",
                Body = "BMR (Basal Metabolic Rate) là tỷ lệ trao đổi chất cơ bản — mức năng lượng tối thiểu cơ thể cần để duy trì sự sống khi nghỉ ngơi hoàn toàn, được tính theo công thức chuẩn Mifflin-St Jeor.\nTDEE (Total Daily Energy Expenditure) là tổng năng lượng tiêu hao hằng ngày, bằng BMR nhân với hệ số vận động (1.2 đến 1.9).\nNguyên tắc quản lý cân nặng:\n- Giảm cân: Nạp < TDEE (sàn an toàn không dưới 1.200 kcal với nữ, 1.500 kcal với nam).\n- Tăng cân: Nạp > TDEE (dư thừa 250 - 500 kcal/ngày).\n- Giữ cân: Nạp = TDEE.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Type = ArticleType.Faq,
                Title = "Nhịn ăn sáng có giúp giảm cân nhanh hơn không?",
                Body = "Việc nhịn ăn sáng bản chất là một dạng thu hẹp khung giờ ăn (Intermittent Fasting - Nhịn ăn gián đoạn 16/8). Yếu tố quyết định bạn giảm được cân hay không vẫn là TỔNG CALO TIÊU THỤ TRONG CẢ NGÀY so với TDEE. Nếu nhịn ăn sáng nhưng ăn bù quá nhiều vào bữa trưa và bữa tối vượt quá TDEE thì bạn vẫn sẽ tăng cân. Hãy chọn khung giờ ăn phù hợp nhất với lối sống và khả năng duy trì lâu dài của bạn.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Type = ArticleType.Article,
                Title = "Nguyên tắc Eat Clean đơn giản cho người bận rộn",
                Body = "Eat Clean không phải là kiêng khem khắc khổ mà là tập trung vào thực phẩm tươi sống, gần với trạng thái tự nhiên nhất:\n1. Hạn chế thực phẩm chế biến sẵn, đồ hộp chứa nhiều chất bảo quản và muối natri.\n2. Chọn nguồn tinh bột phức tạp: gạo lứt, khoai lang, yến mạch thay vì tinh bột tinh chế.\n3. Ưu tiên hấp, luộc, áp chảo ít dầu thay cho chiên rán ngập mỡ.\n4. Tăng cường rau xanh và hoa quả tươi trong từng bữa ăn để cung cấp vitamin và chất xơ dồi dào.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Type = ArticleType.Article,
                Title = "Nên tập thể dục vào thời điểm nào trong ngày để đốt mỡ tốt nhất?",
                Body = "Nhiều nghiên cứu chỉ ra rằng tập cardio nhẹ vào buổi sáng khi bụng rỗng (Fasted Cardio) có thể thúc đẩy cơ thể sử dụng chất béo dự trữ làm năng lượng nhanh hơn. Tuy nhiên, thời điểm tập luyện tốt nhất chính là THỜI ĐIỂM BẠN CÓ THỂ DUY TRÌ ĐỀU ĐẶN NHẤT. Dù tập sáng hay chiều tối, sự kiên trì và tổng mức tiêu hao năng lượng kết hợp cùng chế độ dinh dưỡng hợp lý mới là chìa khóa then chốt.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Type = ArticleType.Article,
                Title = "Hướng dẫn phân bổ tỷ lệ dinh dưỡng đa lượng (Macro) theo mục tiêu",
                Body = "Dinh dưỡng đa lượng gồm Protein (Đạm - 4 kcal/g), Carb (Tinh bột - 4 kcal/g), và Fat (Chất béo - 9 kcal/g):\n- Mục tiêu duy trì sức khỏe: 50% Carb, 20% Protein, 30% Fat.\n- Mục tiêu giảm mỡ / Siết cân: 40% Carb, 30% Protein, 30% Fat (hoặc Low-Carb 25% Carb, 40% Protein, 35% Fat).\n- Mục tiêu tăng cơ / Thể hình: 45% Carb, 30% Protein, 25% Fat.\nViệc tính toán tỷ lệ Macro chuẩn giúp bạn giảm mỡ nhưng không bị teo cơ, da dẻ vẫn săn chắc khỏe mạnh.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            }
        };

        context.Articles.AddRange(articles);
        await context.SaveChangesAsync();
    }

    private static async Task SeedMealPlansAsync(AppDbContext context)
    {
        if (await context.MealPlans.AnyAsync())
        {
            return;
        }

        var phoBo = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Phở bò tái");
        var comGaoLut = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Cơm gạo lứt [Nấu chín]");
        var ucGa = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Ức gà bỏ da [Áp chảo ít dầu]");
        var bongCai = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Bông cải xanh (Súp lơ xanh) [Luộc / Hấp]");
        var khoaiLang = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Khoai lang [Luộc / Hấp]");
        var caHoi = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Cá hồi Atlantic [Áp chảo / Nướng không dầu]");
        var tao = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Táo tươi (cả vỏ)");
        var suaChua = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Sữa chua trắng không đường");
        var comTrang = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Cơm trắng [Nấu chín]");
        var thitBo = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Thịt bò thăn nạc [Áp chảo / Xào ít dầu]");
        var trung = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Trứng gà [Luộc chín]");
        var chuoi = await context.Foods.FirstOrDefaultAsync(f => f.Name == "Chuối tiêu / Chuối già chín");

        // Template 1: Giảm cân 1.500 kcal
        var plan1 = new MealPlan
        {
            UserId = null,
            Name = "Thực đơn Mẫu Giảm Mỡ Lành Mạnh 1.500 kcal",
            GoalType = GoalType.Lose,
            TargetCalories = 1500,
            IsTemplate = true,
            IsPublic = true,
            Explanation = "Thực đơn thâm hụt calo nhẹ nhàng chuẩn khoa học, giàu đạm nạc từ ức gà và cá hồi giúp duy trì khối cơ trong quá trình siết mỡ, kết hợp carb phức hợp từ gạo lứt và khoai lang giúp no lâu.",
            CreatedAt = DateTime.UtcNow
        };
        context.MealPlans.Add(plan1);
        await context.SaveChangesAsync();

        var items1 = new List<MealPlanItem>();
        if (phoBo != null) items1.Add(new MealPlanItem { MealPlanId = plan1.Id, MealType = MealType.Breakfast, FoodId = phoBo.Id, Servings = phoBo.ServingSize, SortOrder = 1 });
        if (comGaoLut != null) items1.Add(new MealPlanItem { MealPlanId = plan1.Id, MealType = MealType.Lunch, FoodId = comGaoLut.Id, Servings = comGaoLut.ServingSize, SortOrder = 2 });
        if (ucGa != null) items1.Add(new MealPlanItem { MealPlanId = plan1.Id, MealType = MealType.Lunch, FoodId = ucGa.Id, Servings = ucGa.ServingSize, SortOrder = 3 });
        if (bongCai != null) items1.Add(new MealPlanItem { MealPlanId = plan1.Id, MealType = MealType.Lunch, FoodId = bongCai.Id, Servings = bongCai.ServingSize, SortOrder = 4 });
        if (khoaiLang != null) items1.Add(new MealPlanItem { MealPlanId = plan1.Id, MealType = MealType.Dinner, FoodId = khoaiLang.Id, Servings = khoaiLang.ServingSize, SortOrder = 5 });
        if (caHoi != null) items1.Add(new MealPlanItem { MealPlanId = plan1.Id, MealType = MealType.Dinner, FoodId = caHoi.Id, Servings = caHoi.ServingSize, SortOrder = 6 });
        if (tao != null) items1.Add(new MealPlanItem { MealPlanId = plan1.Id, MealType = MealType.Snack, FoodId = tao.Id, Servings = tao.ServingSize, SortOrder = 7 });
        if (suaChua != null) items1.Add(new MealPlanItem { MealPlanId = plan1.Id, MealType = MealType.Snack, FoodId = suaChua.Id, Servings = suaChua.ServingSize, SortOrder = 8 });

        context.MealPlanItems.AddRange(items1);

        // Template 2: Tăng cơ 2.200 kcal
        var plan2 = new MealPlan
        {
            UserId = null,
            Name = "Thực đơn Mẫu Tăng Cơ & Vận Động 2.200 kcal",
            GoalType = GoalType.Gain,
            TargetCalories = 2200,
            IsTemplate = true,
            IsPublic = true,
            Explanation = "Thực đơn năng lượng cao giàu đạm (>130g Protein) hỗ trợ phát triển sợi cơ tối đa và phục hồi sau tập luyện nặng, cân bằng nguồn carb hấp thu nhanh và chậm.",
            CreatedAt = DateTime.UtcNow
        };
        context.MealPlans.Add(plan2);
        await context.SaveChangesAsync();

        var items2 = new List<MealPlanItem>();
        if (trung != null) items2.Add(new MealPlanItem { MealPlanId = plan2.Id, MealType = MealType.Breakfast, FoodId = trung.Id, Servings = trung.ServingSize * 2, SortOrder = 1 });
        if (chuoi != null) items2.Add(new MealPlanItem { MealPlanId = plan2.Id, MealType = MealType.Breakfast, FoodId = chuoi.Id, Servings = chuoi.ServingSize, SortOrder = 2 });
        if (comTrang != null) items2.Add(new MealPlanItem { MealPlanId = plan2.Id, MealType = MealType.Lunch, FoodId = comTrang.Id, Servings = comTrang.ServingSize * 1.5m, SortOrder = 3 });
        if (thitBo != null) items2.Add(new MealPlanItem { MealPlanId = plan2.Id, MealType = MealType.Lunch, FoodId = thitBo.Id, Servings = thitBo.ServingSize, SortOrder = 4 });
        if (bongCai != null) items2.Add(new MealPlanItem { MealPlanId = plan2.Id, MealType = MealType.Lunch, FoodId = bongCai.Id, Servings = bongCai.ServingSize, SortOrder = 5 });
        if (comGaoLut != null) items2.Add(new MealPlanItem { MealPlanId = plan2.Id, MealType = MealType.Dinner, FoodId = comGaoLut.Id, Servings = comGaoLut.ServingSize, SortOrder = 6 });
        if (ucGa != null) items2.Add(new MealPlanItem { MealPlanId = plan2.Id, MealType = MealType.Dinner, FoodId = ucGa.Id, Servings = ucGa.ServingSize * 1.5m, SortOrder = 7 });
        if (suaChua != null) items2.Add(new MealPlanItem { MealPlanId = plan2.Id, MealType = MealType.Snack, FoodId = suaChua.Id, Servings = suaChua.ServingSize, SortOrder = 8 });

        context.MealPlanItems.AddRange(items2);
        await context.SaveChangesAsync();
    }
}
