# Báo cáo nâng cấp mục tiêu cân nặng — 07/11/2026

## Sửa lỗi thiếu cột sau nâng cấp

Nếu gặp `Invalid column name 'CaloriePlanJson'`, database đang thiếu migration bổ sung. Program.cs hiện kiểm tra trước seeding: ở Development, nếu chỉ còn migration `20261107105859_AddAdaptiveCaloriePlan`, ứng dụng tự áp dụng migration thêm cột nullable rồi mới xử lý request. Database trống hoặc migration khác không tự chạy; Production vẫn cập nhật bằng EF/script theo hướng dẫn dưới đây. Không reset database.

Dừng và khởi động lại ứng dụng bằng profile http/https (Development). Phiên công cụ đã thử cập nhật trực tiếp nhưng LocalDB không khởi động được trong ngữ cảnh Windows của công cụ; chưa xác minh cập nhật thành công trên database thật. Build Release của bản sửa đạt 0 lỗi, 0 cảnh báo.

## 1. Kết quả và phạm vi

Đã triển khai trực tiếp trên ASP.NET Core 8 MVC / EF Core 8 hiện có. Giữ Bootstrap, Emerald, phân quyền và công thức calo còn lại. Có phân tích năng lượng theo thời hạn, kế hoạch làm quen, đánh giá chu kỳ 14 ngày, xác nhận thay đổi mức điều chỉnh, cập nhật TDEE và ngân sách dùng chung.

**Bản mới cần migration `20261107105859_AddAdaptiveCaloriePlan` trước khi dùng database thật. Chưa áp dụng migration lên SQL Server của người dùng.** Không reset database, không xóa bản ghi, không tạo bảng ứng dụng thứ 18. Host QA và toàn bộ kiểm thử HTTP sử dụng EF InMemory với dữ liệu giả lập riêng.

## 2. Audit: tài liệu, cấu trúc và vấn đề thực tế

Đọc quy tắc `gemini.md`, kiểm tra tài liệu README, AppDbContext, cấu trúc entity/enum và các đường tính toán liên quan. Không tìm thấy hồ sơ bàn giao hoặc đặc tả riêng trong workspace. Entity/enum được đối chiếu theo thuộc tính và ánh xạ; các controller/service/view liên quan được kiểm tra luồng tính toán và dữ liệu.

Những vấn đề tìm thấy:

1. Tốc độ tuần bị làm tròn trước khi quy đổi 7700 kcal/kg, làm lệch mức năng lượng.
2. Kiểm tra cũ cho phép một số trường hợp dưới sàn 1500/1200, dùng mốc 1.2 kg/tuần và chặn cả mục tiêu chậm; giới hạn 750 chưa được áp dụng nhất quán.
3. Có công thức và kiểm tra riêng ở ViewModel, JavaScript của Goal và Onboarding; trạng thái hiển thị có thể khác backend.
4. Có nhãn “an toàn chuẩn”, khẳng định WHO cho toàn bộ kế hoạch và mô tả sức khỏe vượt phạm vi chứng minh của thuật toán.
5. Mục tiêu lưu lượng calo cố định; cập nhật cân nặng/hồ sơ không cập nhật ngân sách theo TDEE mới.
6. Profile tự lập lại kế hoạch theo cân nặng đích, có thể khác Goal đang hoạt động; MealLog có fallback 2000 khác các màn hình khác.
7. Chưa lưu mức điều chỉnh đã xác nhận, mốc đánh giá và lịch sử thay đổi. Các trường hiện tại không đủ khôi phục chính xác sự đồng ý sau khi TDEE đổi.
8. Admin/SystemSelfTest đọc dị ứng và calo cá nhân của tài khoản mẫu, đồng thời có kiểm tra quyền được ghi cứng Passed=true. Đã thay bằng kiểm tra dữ liệu tổng hợp; kiểm thử quyền thật nằm trong test HTTP.
9. WeightLog/Save nhận ID không thuộc người dùng rồi có thể đi vào nhánh tạo mới. Đã trả 404 trước khi xử lý upload/lưu.
10. Cộng calo vận động vào ngân sách có khả năng tính trùng với hệ số hoạt động trong TDEE. Giữ công thức chung và bổ sung giải thích; chưa thay mô hình vận động của hệ thống.

Danh mục đã đọc/đối chiếu (đường dẫn tính từ `D:/DACN/NutriTrackAI`):

```text
gemini.md
README.md
Data/AppDbContext.cs
Data/DbSeeder.cs
Program.cs
NutriTrackAI.csproj
Filters/RequireOnboardingAttribute.cs
Models/Entities/ApplicationUser.cs
Models/Entities/Article.cs
Models/Entities/CaloriePlanState.cs
Models/Entities/ChatMessage.cs
Models/Entities/ChatSession.cs
Models/Entities/Exercise.cs
Models/Entities/ExerciseLog.cs
Models/Entities/Favorite.cs
Models/Entities/Food.cs
Models/Entities/FoodCategory.cs
Models/Entities/Goal.cs
Models/Entities/MealLog.cs
Models/Entities/MealLogDetail.cs
Models/Entities/MealPlan.cs
Models/Entities/MealPlanItem.cs
Models/Entities/UserProfile.cs
Models/Entities/WaterLog.cs
Models/Entities/WeightLog.cs
Models/Enums/ActivityLevel.cs
Models/Enums/ArticleType.cs
Models/Enums/ChatRole.cs
Models/Enums/DietType.cs
Models/Enums/ExerciseIntensity.cs
Models/Enums/Gender.cs
Models/Enums/GoalSafetyLevel.cs
Models/Enums/GoalType.cs
Models/Enums/MealType.cs
Controllers/GoalController.cs
Controllers/ProfileController.cs
Controllers/WeightLogController.cs
Controllers/DashboardController.cs
Controllers/OnboardingController.cs
Controllers/MealPlanController.cs
Controllers/MealLogController.cs
Controllers/FoodController.cs
Controllers/ChatController.cs
Controllers/AccountController.cs
Services/NutritionCalculatorService.cs
Services/INutritionCalculatorService.cs
Services/MealPlannerService.cs
Services/UserContextBuilder.cs
Services/IUserContextBuilder.cs
Services/FoodToolService.cs
Services/GeminiChatService.cs
Services/ChatSafetyFilter.cs
Models/ViewModels/GoalViewModels.cs
Models/ViewModels/OnboardingViewModel.cs
Models/ViewModels/ProfileViewModel.cs
Models/ViewModels/WeightLogViewModels.cs
Models/ViewModels/DashboardViewModels.cs
Models/ViewModels/MealPlanViewModels.cs
Views/Goal/Index.cshtml
Views/Profile/Index.cshtml
Views/Onboarding/Index.cshtml
Views/WeightLog/Index.cshtml
Views/Dashboard/Index.cshtml
Views/MealPlan/Index.cshtml
Views/Goal/Create.cshtml
Views/Shared/_Layout.cshtml
Areas/Admin/Controllers/SystemSelfTestController.cs
```

## 3. Thiết kế và file sửa/thêm

- `NutritionCalculatorService`: BMI/BMR/TDEE, tuổi chính xác và macro. Giữ độ chính xác decimal đến khi hiển thị/lưu calo.
- `GoalPlanningService` + `GoalPolicy`: phân tích hồ sơ, thời gian, năng lượng, BMI và giới hạn; constants tập trung cho 7700, 750/500, sàn calo, chu kỳ đánh giá. Hàm năng lượng và kiểm tra áp dụng được tách riêng.
- `CalorieBudgetService`: nguồn ngân sách hiệu lực cho Goal, Dashboard, Profile, Food Details, MealLog, MealPlanner, UserContextBuilder và FoodToolService.
- `AdaptiveCalorieService`: tính xu hướng qua nhiều ngày, kiểm tra dữ liệu, tạo đề xuất đọc được; không tự ghi mức điều chỉnh.
- `CaloriePlanState`: JSON có version trong một cột nullable của bảng goals, chứa mức điều chỉnh, giai đoạn làm quen, ngày đánh giá, lý do và các lần thay đổi đã xác nhận. Đây là lớp dữ liệu lưu JSON, không phải entity/bảng EF mới.
- Goal/Create sử dụng partial Razor từ backend cho preview. Submit tính lại hoàn toàn; số liệu tính toán và UserId gửi thêm từ client không được dùng để lưu.
- Giữ một Goal Active bằng transaction Serializable khi thay thế mục tiêu ở Goal và Onboarding. Token concurrency trên trạng thái kế hoạch, ngày mong muốn và IsActive giúp phát hiện cập nhật cũ.
- Xác nhận đề xuất kiểm tra lại dữ liệu và fingerprint tại POST, trong transaction; stale/thiếu xác nhận không được áp dụng.
- Package EF/Identity được cố định tại 8.0.33, cùng phiên bản đã có trong môi trường, thay cho wildcard để restore ổn định.

Danh sách file sửa/thêm:

```text
.qa/VISUAL_QA.md
Areas/Admin/Controllers/SystemSelfTestController.cs
Controllers/DashboardController.cs
Controllers/FoodController.cs
Controllers/GoalController.cs
Controllers/MealLogController.cs
Controllers/MealPlanController.cs
Controllers/OnboardingController.cs
Controllers/ProfileController.cs
Controllers/WeightLogController.cs
Migrations/20261107105859_AddAdaptiveCaloriePlan.Designer.cs
Migrations/20261107105859_AddAdaptiveCaloriePlan.cs
Migrations/AppDbContextModelSnapshot.cs
Models/Entities/CaloriePlanState.cs
Models/Entities/Goal.cs
Models/Enums/GoalSafetyLevel.cs
Models/Validation/WeightPrecisionAttribute.cs
Models/ViewModels/GoalViewModels.cs
Models/ViewModels/OnboardingViewModel.cs
Models/ViewModels/ProfileViewModel.cs
Models/ViewModels/WeightLogViewModels.cs
NutriTrackAI.Tests/NutriTrackAI.Tests.csproj
NutriTrackAI.Tests/PlanningTests.cs
NutriTrackAI.Tests/Program.cs
NutriTrackAI.Tests/QaApplication.cs
NutriTrackAI.Tests/WorkflowTests.cs
NutriTrackAI.csproj
Program.cs
README.md
Services/AdaptiveCalorieService.cs
Services/CalorieBudgetService.cs
Services/FoodToolService.cs
Services/GeminiChatService.cs
Services/GoalPlanningService.cs
Services/INutritionCalculatorService.cs
Services/IUserContextBuilder.cs
Services/MealPlannerService.cs
Services/NutritionCalculatorService.cs
Services/UserContextBuilder.cs
Views/Dashboard/Index.cshtml
Views/Goal/Create.cshtml
Views/Goal/Index.cshtml
Views/Goal/_EnergyAnalysis.cshtml
Views/MealPlan/Index.cshtml
Views/Onboarding/Index.cshtml
Views/Profile/Index.cshtml
docs/AddAdaptiveCaloriePlan.sql
docs/WEIGHT_GOAL_UPGRADE.md
wwwroot/css/site.css
wwwroot/js/goal-planning.js
```

## 4. Công thức và logic

```text
BMI = W / (H/100)^2                    (không phân loại người lớn cho dưới 18)
BMR = 10W + 6.25H - 5Age + constant   (+5 / -161 / -78)
TDEE = BMR * ActivityFactor           (1.2 / 1.375 / 1.55 / 1.725 / 1.9)
RequiredAdjustment = abs(W - TargetW) * 7700 / (DesiredWeeks * 7)
LossTarget = TDEE - ConfirmedAdjustment
GainTarget = TDEE + ConfirmedAdjustment
MaintainTarget = TDEE
RemainingCalories = Target - Consumed + Exercise
```

Hằng số -78 của giới tính Khác là quy ước đồ án. BMI là sàng lọc; 7700 là xấp xỉ; không cam kết ngày đạt đích. Các mốc năng lượng là quy tắc hệ thống, không phải chứng nhận/phác đồ của WHO hay Bộ Y tế.

Giảm cân: từ chối thời hạn cần vượt 750 hoặc làm calo dưới sàn nam 1500, nữ 1200. Giới tính Khác dùng sàn thận trọng 1500. Mục tiêu đưa BMI dưới 18.5 bị chặn. Tăng cân kiểm tra giới hạn riêng 500 và ngưỡng BMI nghiệp vụ cũ. Các điều kiện hồ sơ/tuổi/ghi chú sức khỏe được kiểm tra trước khi tạo hay áp dụng kế hoạch.

| Hồ sơ 72 → 65 kg | Mức cần theo thời hạn | Kết quả năng lượng |
| --- | ---: | --- |
| 9 tuần | 855.56 kcal/ngày | Cần điều chỉnh; vượt 750 |
| 11 tuần | 700 | Không vượt 750; vẫn xét sàn và điều kiện khác |
| 14 tuần | 550 | Không vượt 750; mức khởi tạo thông thường tối đa 500 |
| 16 tuần | 481.25 | Trong khoảng thông thường |

Với nam 24 tuổi, 170 cm, Moderate: TDEE 72 kg = 2584.625; TDEE 69 kg = 2538.125. Giữ thâm hụt 500 cho ngân sách mới 2038 kcal/ngày (làm tròn AwayFromZero khi lưu/hiển thị số nguyên). Tuổi dùng ngày Việt Nam và đã xét ngày sinh.

Thời gian mong muốn lưu ở TargetDate. Ngày dự kiến được tính lại từ phần cân nặng còn lại và mức đang được xác nhận; không sửa TargetDate âm thầm. Nút áp dụng thời gian mới chủ động đổi ô nhập.

Kế hoạch làm quen dùng tối đa 250, không tự tăng khi đủ 14 ngày. Preview phân biệt dự kiến giữ mức này với giả thiết được duyệt sang giai đoạn ổn định. Hàm EstimatePlannedDays cộng năng lượng từng giai đoạn: 14 ngày x 250 rồi 500 cho 7 kg cần khoảng 115 ngày = 17 tuần, thay vì giả sử toàn bộ thời gian đều 500. Giai đoạn sau chỉ là giả thiết cho đến khi có đánh giá và xác nhận.

## 5. Luồng sau nâng cấp

1. Người dùng đăng nhập; backend lấy UserId từ Claims, hồ sơ từ database.
2. Chọn giảm/tăng/duy trì, cân nặng đích, số tuần và tùy chọn làm quen. Preview gọi backend, phân loại 4 trạng thái; lỗi mạng hiển thị rõ.
3. POST kiểm tra model/độ chính xác cân nặng (một chữ số thập phân), tính lại hồ sơ và các giới hạn. Chỉ sau khi đạt mới thay thế mục tiêu cũ và lưu mức đã xác nhận.
4. Ghi cân nặng hoặc sửa hồ sơ/vận động sẽ tính lại TDEE. Giữ mức điều chỉnh đã đồng ý và thông báo ngân sách mới; nếu hồ sơ không còn phù hợp thì tạm ngừng thâm hụt/thặng dư và giải thích việc dùng TDEE.
5. Đến kỳ đánh giá, cần ít nhất 2 ngày cân trong mỗi nửa chu kỳ 14 ngày, trải dài tối thiểu 10 ngày. Một ngày nhiều lần cân được gộp. Không đủ dữ liệu thì không kết luận plateau.
6. Xu hướng quá nhanh có thể đề xuất giảm mức điều chỉnh. Xu hướng chậm chỉ xem xét tăng sau 2 chu kỳ, đủ ngày ăn ở cả hai chu kỳ, mức ghi nhận gần ngân sách và đủ thời gian từ lần thay đổi gần nhất. Dữ liệu tự ghi không chứng minh tuân thủ chính xác.
7. Người dùng áp dụng (có checkbox xác nhận), giữ mức hiện tại, hoặc chủ động kéo dài thời gian. Mọi đề xuất được kiểm tra lại khi POST; mức 650/700 không phải bắt buộc. Khi xu hướng đạt đích, hệ thống đề xuất chuyển duy trì và chờ xác nhận.
8. Dashboard/MealPlanner/Chatbot lấy ngân sách hiệu lực chung. Gợi ý thực đơn mới dùng 25/35/30/10, giữ bộ lọc dị ứng, món không thích, chế độ ăn. Thực đơn đã lưu và nhật ký cũ không bị sửa.
9. Context và fallback Chatbot nêu TDEE, giai đoạn, mức đang áp dụng, đề xuất chưa áp dụng, giới hạn và mốc ngày từ service; giữ bộ lọc y tế và disclaimer. Chatbot không có quyền sửa mục tiêu.

## 6. Kiểm thử và build đã chạy

**69 test: 69 Passed, 0 Failed, 0 Skipped.** Kết quả máy đọc tại `NutriTrackAI.Tests/TestResults/goal-upgrade.trx`.

Các kiểm tra đã thực hiện:

```powershell
dotnet restore -p:NuGetAudit=false
dotnet build NutriTrackAI.Tests/NutriTrackAI.Tests.csproj -c Release --no-restore -p:UseSharedCompilation=false
dotnet test NutriTrackAI.Tests/NutriTrackAI.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=goal-upgrade.trx"
dotnet ef migrations has-pending-model-changes --configuration Release --no-build
```

- Restore thành công. Mạng NuGet bị từ chối; lần restore test ban đầu thất bại, sau đó dùng package cache và phiên bản cố định để hoàn tất. Không bỏ qua thiếu package.
- Build Release thành công: **0 warnings, 0 errors**. Build Debug ban đầu bị khóa tệp bởi tiến trình NutriTrackAI đang chạy; đã dùng Release để không dừng ứng dụng của người dùng.
- EF báo không còn thay đổi model ngoài migration.
- Đã tạo script SQL idempotent và kiểm tra script chỉ AddColumn + ghi migration history; chưa chạy script trên database thật.

Ánh xạ yêu cầu kiểm thử 01–20:

| Yêu cầu | Bằng chứng tự động |
| --- | --- |
| 01–03 BMI/BMR/TDEE | PlanningTests, decimal và expected chính xác |
| 04–07 các thời hạn | Theory 9/11/14/16, giữ số tuần và chặn backend |
| 08 đổi cân nặng | Unit + HTTP WeightLog/Save 72 → 69, giữ 500 |
| 09–10 650/700 | Unit + HTTP chưa xác nhận không đổi, xác nhận mới áp dụng |
| 11–13 giới hạn/sàn | Unit + HTTP từ chối 751, sàn nam/nữ |
| 14 thiếu dữ liệu | Một lần cân, nhiều lần cùng ngày, thiếu nhật ký ăn không tăng |
| 15 duy trì | Unit + HTTP Goal, đăng ký → Onboarding duy trì |
| 16 tăng cân | Chính sách thặng dư độc lập |
| 17 đồng bộ | Service budget/context/FoodTools/MealPlanner; render HTTP các trang; Profile sửa vận động và WeightLog cập nhật |
| 18 bảo mật | HTTP User A sửa Goal B trả 404; Admin không đọc dữ liệu mẫu qua SelfTest; xác thực và anti-forgery |
| 19 dữ liệu client | HTTP giả UserId/StartWeight/Height/TDEE/CalculatedCalories vẫn lưu số backend |
| 20 hồi quy | Đăng ký → Onboarding → Dashboard/MealLog; render Profile/WeightLog/MealPlan/Chat; giữ lịch sử bữa ăn và bộ lọc thực đơn |

Bổ sung: fingerprint stale, quá nhanh/đúng kế hoạch, lịch sử xác nhận, kế hoạch cũ vượt ngưỡng bị tạm ngừng, nhiều giai đoạn, giữ mức khi đánh giá trước đó, không dùng xu hướng cũ ngay sau đổi kế hoạch, chuyển duy trì và fallback Chatbot giữ disclaimer.

HTTP dùng Kestrel thật và Identity/cookie/anti-forgery thật; database trong test là EF InMemory. Không coi các test này là bằng chứng đã chạy SQL Server concurrency, gửi email hoặc gọi Gemini thật.

## 7. Giao diện đã kiểm tra

Đã chạy host QA riêng tại loopback, chỉ dữ liệu giả lập, và đã dừng host sau kiểm tra. Đã xem ảnh Goal/Create và Goal/Index ở **1440, 768, 390 px**; không thấy tràn ngang ở mobile. Sửa trạng thái chọn loại mục tiêu và tăng tương phản cảnh báo sau vòng xem ảnh đầu.

Thao tác browser đã xác nhận: nhập 9 tuần → giữ nguyên 9, báo 855.56 và khóa tạo; bấm áp dụng → đổi thành 16; bật làm quen → 250, ước tính giữ mức 31 tuần / giả thiết hai giai đoạn 17 tuần; xác nhận 650 trên dữ liệu QA → ngân sách 1935, kỳ tiếp theo 21/11/2026.

Ảnh nằm trong `.qa/ui-create/` và `.qa/ui-index/`. `inspect.mjs` bị spawn EPERM; dùng trình duyệt tích hợp để kiểm tra thủ công. Không tuyên bố inspector đạt 0 FAIL hoặc có so sánh ảnh trước nâng cấp.

## 8. Giới hạn còn lại

- Chưa áp dụng migration và chưa chạy nghiệm thu trên SQL Server thật. Cần thực hiện trước khi đưa bản mới vào ứng dụng đang dùng.
- Không gọi Gemini/email thật. Đã kiểm tra context, fallback, safety filter và các trang Chat; chất lượng câu trả lời từ mô hình ngoài cần kiểm tra sau khi cấu hình API.
- Dữ liệu y tế chưa có cấu trúc sàng lọc chuyên sâu. Chính sách hiện tại thận trọng: mọi HealthNotes không rỗng đều yêu cầu hỗ trợ chuyên môn và không tạo điều chỉnh calo tự động; chưa thể xác nhận đủ điều kiện y tế bằng ghi chú tự do. Người dưới 18 không được lập kế hoạch calo người lớn.
- Hệ thống nhắc/đánh giá khi mở Goal hoặc lấy context, không có job nền/email nhắc lịch.
- Nhật ký tự ghi không kiểm chứng lượng ăn/tiêu hao thực tế. Quy đổi 7700 và dự kiến tuyến tính chỉ phục vụ đồ án, chưa thay thế mô hình chuyển hóa động.
- Dữ liệu cũ không có CaloriePlanJson dùng mức ước tính từ WeeklyRateKg (vốn đã làm tròn). Hiển thị rõ đây là kế hoạch cũ cần đánh giá; trường hợp vượt giới hạn được tạm ngừng. Không thể khôi phục chính xác mức đã chọn nếu dữ liệu cũ chưa từng lưu nó.
- Giữ công thức cộng ExerciseCalories vì tính tương thích. Nguy cơ tính trùng vận động đã được giải thích; chưa có mô hình tách vận động nền và buổi tập bổ sung.
- Không có Git repository trong workspace nên chưa tạo commit/diff Git. Danh sách file thay đổi được ghi ở mục 3.

## 9. Cách triển khai và demo

1. Dừng bản Debug đang chạy khi bạn sẵn sàng dùng code mới; giữ database hiện tại.
2. Xem script `docs/AddAdaptiveCaloriePlan.sql`. Áp dụng migration bổ sung bằng EF trên đúng connection string, hoặc chạy script này trên database đã có InitialCreate:

```powershell
dotnet ef database update 20261107105859_AddAdaptiveCaloriePlan --configuration Release
```

3. Chạy `dotnet run --configuration Release --launch-profile http` sau migration. Truy cập `/Goal/Create` bằng tài khoản đã hoàn tất hồ sơ.
4. Với hồ sơ nam 24 tuổi, cao 170 cm, 72 kg, Moderate, không có ghi chú sức khỏe: chọn giảm → 65 kg → 9 tuần. Kiểm tra cảnh báo 855.56, tối thiểu toán học 11 và đề xuất thông thường 16; ô số tuần vẫn là 9.
5. Bấm áp dụng thời gian đề xuất; kiểm tra 16 tuần. Bật/tắt làm quen để so sánh mức 250 với mức theo thời hạn. Xác nhận tạo mục tiêu.
6. Mở `/Goal` xem ngày mong muốn, ngày dự kiến, TDEE, mức điều chỉnh và chu kỳ. Nếu chỉ có ít lần cân thì thông báo thiếu dữ liệu là đúng; không tự sửa ngày hệ thống để ép đề xuất.
7. Ghi cân nặng 69 kg ở `/WeightLog`; nếu đang giữ 500, ngân sách sẽ gần 2038. So sánh Dashboard, Profile, MealPlan và hỏi Chatbot về TDEE/calo còn lại/giai đoạn.
8. Khi có đủ dữ liệu qua nhiều tuần, xem đề xuất và chọn áp dụng/giữ nguyên/kéo dài. 650/700 chỉ được áp dụng sau xác nhận và khi các phép kiểm tra đều đạt.

Để chạy lại demo bằng dữ liệu giả lập (không dùng database thật):

```powershell
dotnet run --project NutriTrackAI.Tests/NutriTrackAI.Tests.csproj -c Release --no-build -- --serve
```

Mở `http://127.0.0.1:5168/qa/signin`. Route đăng nhập nhanh chỉ có trong host QA của project test, không tồn tại trong ứng dụng production. Dừng host bằng Ctrl+C.

Nguồn tham khảo phạm vi thuật toán: [NIDDK — Body Weight Planner](https://www.niddk.nih.gov/health-information/weight-management/body-weight-planner) mô tả lập kế hoạch năng lượng/cân nặng; [CDC — BMI trẻ em và thanh thiếu niên](https://www.cdc.gov/bmi/child-teen-calculator/bmi-categories.html) phân biệt đánh giá BMI theo tuổi. Các giới hạn 750/1500/1200 và các giai đoạn trong code là chính sách đồ án theo yêu cầu, không được suy diễn là các nguồn này xác nhận toàn bộ thuật toán.
