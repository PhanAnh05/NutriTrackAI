# Bàn giao NutriTrackAI cho AI tiếp quản

## Preview mục tiêu và phương án thời gian · 19/11/2026

- Tạo mục tiêu và Onboarding dùng cùng `GoalPlanningService` để xem trước và kiểm tra khi lưu. Với cân đích trống, sai chiều hoặc thay đổi dưới 1 kg, màn hình nhắc nhập cân đích phù hợp. Khi sàng lọc chặn kế hoạch, màn hình chỉ hiện lý do và liên kết cập nhật. Trang sàng lọc kiểm tra `returnUrl` bằng `Url.IsLocalUrl`. Sau khi quay lại, preview chạy lại và dùng bản nháp của tab nếu có. Bản nháp có thời hạn 20 phút và lưu các trường cơ bản của mục tiêu hoặc Onboarding.
- Với cân đích hợp lệ, máy chủ đánh giá các mốc giảm **0,25 / 0,5 / 0,75 kg/tuần** hoặc tăng **0,2 / 0,3 / 0,4 kg/tuần**, cùng thời hạn ngắn nhất theo hồ sơ. Mỗi phương án làm tròn lên theo tuần nguyên và được kiểm bằng cùng hàm phân tích lúc lưu. Phương án trùng số tuần được gộp, ưu tiên nhãn **Nhanh nhất cho phép** và giữ cờ **Đề xuất**. Khi phương án đề xuất vượt trần, biểu mẫu chọn phương án khả dụng đầu tiên. Nhập số tuần thủ công giữ giá trị do người dùng chọn. Thẻ hiển thị ngân sách ở mức đầy đủ, kèm ngân sách riêng trong hai tuần đầu nếu bật làm quen.
- Khối chọn thời gian dùng radio gốc, hỗ trợ phím mũi tên. Preview đợi 300 ms sau khi nhập và hủy yêu cầu cũ. Khoản ngân sách ở thẻ, lý do không khả dụng, nhãn vùng và yêu cầu xác nhận vùng Mạnh đến từ máy chủ.
- Khi hồ sơ đã sàng lọc đổi tập hợp tình trạng thuộc tầng `NeedsDoctor`, máy chủ đặt `DoctorAwareOfPlan` về `false` và yêu cầu xác nhận lại. Thay đổi ở tầng khác giữ hiệu lực của xác nhận đang được chọn. Chatbot có lời nhắc dưới ô nhập về thông tin cá nhân nhạy cảm.

| Phần | Tệp thay đổi |
| --- | --- |
| Preview, lưu và sàng lọc | `Controllers/GoalController.cs`, `Controllers/OnboardingController.cs`, `Controllers/ProfileController.cs`, `Services/GoalPlanningService.cs`, `Services/INutritionCalculatorService.cs`, `Services/GoalPolicy.Health.cs` |
| Mô hình biểu mẫu | `Models/ViewModels/GoalViewModels.cs`, `OnboardingViewModel.cs`, `HealthScreeningPageViewModel.cs`, `HealthScreeningForm.cs` |
| Giao diện | `Views/Goal/Create.cshtml`, `Views/Goal/_EnergyAnalysis.cshtml`, `Views/Onboarding/Index.cshtml`, `Views/Profile/Screening.cshtml`, `Views/Shared/_PaceChoices.cshtml`, `Views/Chat/Index.cshtml`, `wwwroot/css/site.css`, `wwwroot/js/goal-planning.js`, `pace-picker.js`, `goal-draft.js` |
| Kiểm thử | `NutriTrackAI.Tests/GoalTimingTests.cs`, `WorkflowTests.cs`, `PlanningTests.cs`, `Phase3PolicyTests.cs` |

- Các test mới kiểm tra bảng thời gian và ngân sách, giới hạn 104 tuần, gộp thẻ, ngân sách sau lưu, kết quả giống nhau ở hai preview, ẩn bảng số, điều hướng an toàn và xác nhận lại của bác sĩ. Test cũ được cập nhật để dùng lời nhắc cân đích mới, trường `WeeklyRateKg` và trạng thái không khả dụng thay cho cách kẹp tuần của nhóm nút cũ.
- Kết quả build và test mặc định theo nhóm lần lượt là **219/219**, **224/224** và **226/226** test đạt. Build ứng dụng có 0 lỗi và 0 cảnh báo. Dự án test có cảnh báo `NU1900` khi không truy cập được nguồn dữ liệu cảnh báo NuGet.
- Edge headless đã kiểm Goal/Create và Onboarding ở 375 px và 1280 px cho hồ sơ hợp lệ, bị chặn vì sàng lọc và hồ sơ cần bác sĩ đã xác nhận. Cả sáu trường hợp ở hai kích thước đều không tràn ngang. Kiểm thử dùng host QA với EF InMemory. LocalDB, SQL Server thật và các trình duyệt khác nằm ngoài lượt kiểm này.

Mục [Triển khai đợt 3](#triển-khai-đợt-3) mô tả chính sách BMI, sàng lọc và khẩu phần làm nền cho cập nhật này. [Bản bàn giao ngày 16/11/2026](HANDOFF_CUOC_TRO_CHUYEN_MOI.md) và các báo cáo bên dưới ghi lại trạng thái tại các mốc trước.

## Triển khai đợt 3

### A · Trần giảm cân theo BMI

- Với BMI dưới 23, từ 23 đến dưới 25, và từ 25 trở lên, trần lần lượt là `min(500, 20% × TDEE, 11 × kg)`, `min(750, 25% × TDEE, 11 × kg)`, và `min(1.000, 35% × TDEE, 11 × kg)` kcal/ngày. Sàn calo là 1.200 cho nữ và 1.500 cho nam hoặc giới tính khác. Thặng dư cơ sở vẫn là `min(500, 20% × TDEE)`.
- Khi giữ mức thâm hụt của kế hoạch đang chạy, xác định bậc BMI bằng cân lớn hơn giữa cân hiện tại và cân lúc bắt đầu. Dùng cân hiện tại cho TDEE và giới hạn `11 × kg`. Dung sai 10% không vượt qua sàn calo hoặc trần sức khỏe 350 kcal. Mức điều chỉnh mới do check-in hoặc đánh giá xu hướng đề xuất dùng trần cơ sở, với bậc BMI của kế hoạch đang chạy.
- Vùng giảm cân gồm Rất chậm dưới 0,25 kg/tuần, Nhẹ nhàng từ 0,25 đến dưới 0,5, Tiêu chuẩn từ 0,5 đến 0,75, Mạnh trên 0,75 nếu còn trong trần, và Vượt giới hạn. Chỉ vùng Mạnh cần tích xác nhận. Nút tham chiếu dùng 550 kcal/ngày trong giới hạn hồ sơ. Nút 0,75 kg/tuần dùng số tuần nguyên làm tròn lên, tối thiểu 2 tuần.
- Preview ở Tạo mục tiêu giữ phản hồi HTML mặc định, có thể trả JSON với `Accept: application/json`. Onboarding giữ JSON. Máy chủ cung cấp vùng tốc độ, nhãn, tốc độ tối đa, lựa chọn nhanh và trạng thái yêu cầu xác nhận.

| Hồ sơ giả lập | Đợt 2 | Đợt 3 |
| --- | --- | --- |
| U · 85 → 80 kg, 170 cm, TDEE 2.430,31 | Trần 607,58 kcal/ngày, tối thiểu 10 tuần | Trần 850,61 kcal/ngày, tối thiểu 7 tuần. 7 tuần thuộc vùng Tiêu chuẩn, ngân sách 1.645 kcal. 30 tuần có cảnh báo Rất chậm. |
| B · 85 → 75 kg, 175 cm, TDEE 2.438,91 | Trần 609,73 kcal/ngày, tối thiểu 19 tuần | Trần 853,62 kcal/ngày, tối thiểu 13 tuần. 13 tuần thuộc vùng Mạnh, ngân sách 1.593 kcal. |
| A · 60 → 56 kg, 160 cm, TDEE 2.013,45 | Trần 503,36 kcal/ngày, tối thiểu 9 tuần | 9 tuần vẫn hợp lệ với 1.525 kcal. 8 tuần vượt trần. |
| L · 55 → 52 kg, 160 cm, TDEE 1.717,38 | Trần 429,34 kcal/ngày, tối thiểu 8 tuần | Trần 343,48 kcal/ngày, tối thiểu 10 tuần. |
| U · Cao huyết áp, bác sĩ đã biết | Yếu tố sức khỏe cần đánh giá chặn kế hoạch tự động. | Trần 350 kcal/ngày. 14 và 15 tuần vượt trần. 16 tuần hợp lệ với 2.087 kcal và tốc độ 0,3125 kg/tuần. |

Ví dụ dung sai trong đặc tả có xung đột với sàn calo. Hồ sơ U ở 72 kg, Light, trừ 780 từ TDEE 2.251,56 cho ngân sách 1.471,56 kcal. Mã ưu tiên sàn 1.500 và chặn ca này. 750 kcal hợp lệ cho kế hoạch đang chạy và bị chặn nếu tạo mới tại 72 kg. Ca riêng với mức Moderate kiểm tra 850 kcal chỉ hợp lệ nhờ dung sai 10%.

### B · Sàng lọc sức khỏe

- `GoalPolicy.EvaluateHealth` dùng tầng nghiêm nhất trong số các mục đã chọn. Dưới 18 tuổi, dùng insulin hoặc thuốc hạ đường huyết, bệnh thận mạn, tiền sử rối loạn ăn uống, mang thai hoặc cho con bú, và bệnh khác thuộc Blocked. Tiểu đường, cao huyết áp, bệnh tim mạch và gout thuộc NeedsDoctor. Khi người dùng xác nhận bác sĩ đã biết, mức điều chỉnh tối đa là 350 kcal/ngày cho giảm và tăng, kèm đề xuất làm quen. Dạ dày và suy giáp thuộc Caution. Suy giáp giữ nguyên công thức TDEE.
- Onboarding yêu cầu chọn tình trạng hoặc xác nhận không có bệnh nền trong danh sách. Hồ sơ không đủ điều kiện vẫn có thể hoàn thành Onboarding để dùng nhật ký. Hồ sơ cũ chưa sàng lọc và ghi chú rỗng dùng được kế hoạch, kèm lời mời cập nhật. Ghi chú cũ có nội dung hoặc câu trả lời cũ từng báo rủi ro yêu cầu sàng lọc lại. Sau khi sàng lọc mới, các lựa chọn và xác nhận bác sĩ là đầu vào của chính sách. Dashboard chỉ hiển thị lý do tạm ngừng chung.
- `HealthScreeningAt` lấy từ `GoalPolicy.Now`. Lịch sử có migration `20261110053547_AddHealthScreening` từng được áp dụng cho cột `HealthScreeningJson`. Migration mới là `20261117025750_AddStructuredHealthScreening` để giữ nguyên ID cũ và tránh trùng tên. Bản [SQL nâng cấp](migrations/AddHealthScreening.sql) chỉ thêm `MedicalConditions` mặc định 0, `DoctorAwareOfPlan` mặc định false, `HealthScreeningAt` mặc định NULL và ghi lịch sử EF. Các trường ghi chú và sàng lọc cũ được giữ.

Chạy từ thư mục gốc với tài khoản Windows có LocalDB. Lệnh dưới đây dành cho người áp dụng sau khi xác nhận tên database phát triển.

```powershell
$backupDirectory = New-Item -ItemType Directory -Path .db-backups -Force
$backupPath = Join-Path $backupDirectory.FullName "NutriTrackAIDb-before-structured-health-$(Get-Date -Format yyyyMMdd-HHmmss).bak"
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -b -Q "BACKUP DATABASE [NutriTrackAIDb] TO DISK = N'$backupPath' WITH COPY_ONLY, CHECKSUM; RESTORE VERIFYONLY FROM DISK = N'$backupPath' WITH CHECKSUM;"
if ($LASTEXITCODE -ne 0) { throw "Sao lưu hoặc kiểm tra bản sao lưu thất bại." }
dotnet ef database update 20261117025750_AddStructuredHealthScreening -- --environment Development
```

Đợt này đã tạo và đọc bản SQL, đồng thời xác nhận EF không còn thay đổi model chưa có migration. Chưa thực thi SQL, sao lưu hoặc chạy cập nhật database trên LocalDB.

### C · Macro, natri và thực đơn

- Dashboard, Nhật ký ăn, Thực đơn, Onboarding, chatbot và công cụ thực phẩm dùng cùng phép tính. Trần đạm với gout là 1,2 g/kg. Khoảng tinh bột với tiểu đường là 40–45% năng lượng và chia mục tiêu tinh bột đều giữa các bữa. Trần chất béo là 35%. Số gram được làm tròn khi hiển thị.
- Với 2.087 kcal và 85 kg, tỷ lệ gốc 30/40/30 cho khoảng 156,5/208,7/69,6 g đạm, tinh bột và béo. Gout cho 102,0/263,2/69,6 g. Với tiểu đường và tỷ lệ gốc 25/50/25, kết quả là 143,5/234,8/63,8 g. Nếu cùng có gout và tiểu đường, tổng tối đa tại 85 kg với ngân sách 2.087 kcal chỉ là 2.077,6 kcal. Ứng dụng hiện thông báo và giữ nguyên ngân sách thay vì hiển thị mục tiêu macro trái các giới hạn.
- Dữ liệu natri có sẵn trong `Food` và snapshot `MealLogDetail`. Dashboard và Nhật ký ăn theo dõi mốc 2.000 mg với hồ sơ phù hợp. Thực đơn ưu tiên món ít natri, dùng tối thiểu bốn bữa khi có cờ dạ dày và hiện lưu ý đối chiếu nhãn. Natri trong danh mục và khẩu phần không đủ để xác nhận mọi thực đơn đạt mốc. Các tag GI, purine và chua cay là chỉ dẫn cho Gemini vì chưa có dữ liệu để lọc cứng. Các hồ sơ đều có lời miễn trừ y tế.

### D · Gemini và dữ liệu hồ sơ

- `AiPromptBuilder` là điểm ghép system prompt duy nhất. `UserContextBuilder` định dạng số đã tính. `MealPlannerService` và `FoodToolService` tính tại máy chủ. Prompt kể rõ khi kế hoạch tạm ngừng. Luồng chat tạm ngừng chỉ tóm tắt nhật ký tại chỗ. Fallback dùng số liệu từ cùng các dịch vụ.
- Phiên bản cũ gửi họ tên, văn bản dị ứng và món không thích từ hồ sơ. Phiên bản mới gửi số liệu nhật ký và kế hoạch phục vụ câu hỏi cùng tag ăn uống đã dịch. Nhóm dị ứng được chuẩn hóa để tránh gợi ý sai thành phần. Dị ứng không nhận dạng được chỉ tạo cờ chung, văn bản gốc vẫn dùng để đối chiếu trong máy chủ. Payload tự động không lấy họ tên, email, ghi chú sức khỏe, danh sách bệnh hoặc câu trả lời sàng lọc. Nội dung chat người dùng tự nhập có thể được gửi khi gọi Gemini. Lịch sử chuyển tiếp bỏ lượt cũ có nguy cơ chứa dữ liệu hồ sơ và các lượt trước có cờ rủi ro y tế.

### Phạm vi kiểm thử và đề xuất

- Lệnh mặc định `dotnet build` và `dotnet test .\NutriTrackAI.Tests\NutriTrackAI.Tests.csproj` áp dụng cho cả bốn nhóm. `Group3` từng là tham số build của đợt 2. Hai project hiện không khai báo cấu hình riêng đó. Có thể chạy bộ test đầy đủ bằng cấu hình mặc định.
- Kết quả sau từng nhóm là A **189/189**, B **198/198**, C **206/206**, D **208/208**. Lượt kiểm tra cuối đạt **209/209** sau khi bổ sung ca ưu tiên natri và kiểm tra lại giao diện. Mỗi lượt build ứng dụng có 0 lỗi và 0 cảnh báo. Dự án test báo `NU1900` vì môi trường không truy cập được nguồn dữ liệu cảnh báo NuGet.
- Thêm `Phase3PolicyTests.cs` cho bảng thời hạn, mốc tốc độ, dung sai, trần theo tầng, tổ hợp bệnh, macro và nội dung prompt. Mở rộng `WorkflowTests.cs` cho cả hai dạng preview, xác nhận 0,75, sàng lọc và quyền sở hữu, hiển thị natri, bốn bữa và thứ tự ưu tiên natri, cùng payload Gemini qua transport giả. Trong `PlanningTests.cs` và `WorkflowTests.cs`, cập nhật kỳ vọng cũ về trần 750, ngưỡng 0,5, lựa chọn tham chiếu khi chạm sàn và điều kiện mở khóa theo ghi chú hoặc câu trả lời cũ. Giữ các ca kiểm tra chặn quá giới hạn và điều chỉnh dữ liệu giả lập cho phù hợp với chính sách mới.
- Test chính sách dùng hồ sơ giả lập. Test HTTP dùng EF InMemory gồm Onboarding, Hồ sơ, quyền sở hữu, antiforgery, nhật ký, sàng lọc, và payload Gemini qua transport giả. Các phép kiểm này chưa kiểm chứng giao dịch, migration trên SQL Server thật hoặc phản hồi Gemini thật.
- Edge headless kiểm Onboarding, Tạo mục tiêu, Hồ sơ, Dashboard, Nhật ký ăn và Thực đơn ở 375 px và 1280 px trên host InMemory. Kiểm tra cảnh báo Rất chậm không chặn, nút 0,75 bật/tắt theo hồ sơ, lựa chọn sàng lọc loại trừ nhau, xác nhận bác sĩ, lưu kế hoạch 2.087 kcal, thanh natri và thực đơn bốn bữa. Các màn hình đã kiểm không tràn ngang.
- Các đề xuất để sau gồm chế độ cho người dưới 18 tuổi có BMI theo tuổi và giới, cột GI và purine, ước tính TDEE từ nhật ký và cân, chuyển duy trì theo giai đoạn, cân trung bình bảy ngày, giới hạn tuổi trên và sàng lọc thuốc đang dùng.

### Danh mục tệp đợt 3

| Thư mục | Tệp thêm hoặc sửa |
| --- | --- |
| `Services/` | `GoalPlanningService.cs`, `GoalPolicy.Health.cs`, `GoalPolicy.Diet.cs`, `CalorieBudgetService.cs`, `AdaptiveCalorieService.cs`, `NutritionCalculatorService.cs`, `INutritionCalculatorService.cs`, `MealPlannerService.cs`, `FoodToolService.cs`, `IFoodToolService.cs`, `UserContextBuilder.cs`, `IUserContextBuilder.cs`, `GeminiChatService.cs`, `AiPromptBuilder.cs`, `AllergenGroups.cs` |
| `Controllers/` | `GoalController.cs`, `OnboardingController.cs`, `ProfileController.cs`, `DashboardController.cs`, `MealLogController.cs`, `MealPlanController.cs` |
| `Models/` | `Entities/UserProfile.cs`, `Enums/DietType.cs`, `Enums/MedicalConditions.cs`, `ViewModels/HealthScreeningForm.cs`, `ViewModels/OnboardingViewModel.cs`, `ViewModels/ProfileViewModel.cs`, `ViewModels/DashboardViewModels.cs`, `ViewModels/MealLogViewModels.cs`, `ViewModels/MealPlanViewModels.cs` |
| `Views/` | `Goal/Create.cshtml`, `Goal/Index.cshtml`, `Goal/_EnergyAnalysis.cshtml`, `Onboarding/Index.cshtml`, `Profile/Index.cshtml`, `Dashboard/Index.cshtml`, `MealLog/Index.cshtml`, `MealPlan/Index.cshtml`, `Chat/Index.cshtml`, `Shared/_PaceChoices.cshtml`, `Shared/_SodiumProgress.cshtml`, `Shared/EditorTemplates/HealthScreeningForm.cshtml`. Gỡ editor cũ `HealthScreeningAnswers.cshtml`. |
| `wwwroot/js/` | `health-screening.js`, `pace-picker.js`, `goal-planning.js` |
| EF và SQL | `Data/AppDbContext.cs`, `Migrations/20261117025750_AddStructuredHealthScreening.cs`, tệp designer cùng migration, `Migrations/AppDbContextModelSnapshot.cs`, `docs/migrations/AddHealthScreening.sql` |
| Kiểm thử và bàn giao | `NutriTrackAI.Tests/Phase3PolicyTests.cs`, `NutriTrackAI.Tests/PlanningTests.cs`, `NutriTrackAI.Tests/WorkflowTests.cs`, `docs/AI_HANDOFF.md` |

Quy tắc đồng bộ cân nặng đã triển khai ở Nhóm 1 đợt 2 nằm trong [Đặc tả đồng bộ cân nặng](WEIGHT_SYNC_PHASE_2.md). Ưu tiên đặc tả này khi đọc các đoạn bàn giao cũ về ghi, sửa và xóa cân.

Thay đổi về nhãn calo, trạng thái tạm ngừng, phân trang cân nặng và ngày tiếng Việt nằm trong [Báo cáo Nhóm 2](UI_PHASE_2_REPORT.md).

Quy tắc chọn tốc độ theo số tuần và xác nhận khi giảm nhanh hơn mức tham chiếu nằm trong [Báo cáo Nhóm 3](GOAL_PACE_PHASE_2_REPORT.md).

Cập nhật ngày **17/11/2026** theo mã nguồn và kết quả kiểm tra trong `D:\DACN\NutriTrackAI`. Các báo cáo theo mốc ghi lại trạng thái tại thời điểm nghiệm thu tương ứng.

## 1. Điểm bắt đầu

NutriTrackAI là đồ án web tiếng Việt giúp người dùng quản lý cân nặng và dinh dưỡng. Luồng chính là đăng ký → hoàn tất hồ sơ → chọn mục tiêu → xem ngân sách calo → ghi bữa ăn, nước, vận động và cân nặng → theo dõi tiến độ hoặc hỏi trợ lý AI. Có khu vực quản trị riêng cho danh mục, thực phẩm, thực đơn mẫu, bài viết, tài khoản và thống kê.

- **Mã nguồn** dùng C#, ASP.NET Core MVC, Razor Views, .NET 8, EF Core 8.0.33 và SQL Server LocalDB. Xác thực dùng ASP.NET Core Identity với khóa `int`, cookie và vai trò `Admin`/`User`.
- **Giao diện** dùng Bootstrap có sẵn trong `wwwroot/lib`, Bootstrap Icons, CSS riêng, JavaScript thuần và Chart.js. Một số icon, font và biểu đồ lấy từ CDN. Thao tác cập nhật nhanh dùng `fetch`.
- **Nghiệp vụ đã có** gồm kế hoạch calo theo mục tiêu, TDEE cập nhật theo hồ sơ, đánh giá theo chu kỳ 14 ngày và xác nhận trước khi đổi kế hoạch. Đợt cập nhật 09/11 đồng bộ ngày nghiệp vụ Việt Nam, thống nhất phần calo vận động được cộng lại và điều chỉnh giới hạn kế hoạch theo hồ sơ.
- **Dashboard** đã được sắp xếp lại ở phiên trước, với số calo còn lại nổi bật và nút ghi nhanh nước. Đợt nghiệp vụ 09/11 cập nhật phần ngày và diễn giải số liệu trong các view.
- **Kiểm thử** được mô tả ở mục 6. Thư mục hiện tại không có metadata Git khả dụng để xác định branch, commit hoặc diff.

## 2. Bản đồ mã nguồn

| Điểm vào | Mã liên quan | Trách nhiệm |
| --- | --- | --- |
| `/`, `/Home/Articles`, `/Home/Privacy` | `Controllers/HomeController.cs`, `Views/Home/` | Trang giới thiệu, nội dung đã công bố, thông tin quyền riêng tư. |
| `/Account/Login`, `/Account/Register` | `Controllers/AccountController.cs`, `Views/Account/`, `Services/EmailSender.cs` | Đăng ký, đăng nhập, đăng xuất và đặt lại mật khẩu. Người dùng chưa có hồ sơ được dẫn tới onboarding. |
| `/Onboarding` | `OnboardingController`, `OnboardingViewModel`, `Views/Onboarding/Index.cshtml` | Luồng tạo hồ sơ. `CalculatePreview` trả phân tích trước khi `Complete` lưu. Preview cho phép thiếu trường nước tùy chọn. |
| `/Dashboard` | `DashboardController`, `DashboardViewModels.cs`, `Views/Dashboard/Index.cshtml` | Tổng hợp calo, BMI/BMR/TDEE, mục tiêu, macro, nước, biểu đồ cân nặng và món gợi ý. `POST /Dashboard/QuickAddWater` ghi nhanh lượng nước. |
| `/Goal`, `/Goal/Create` | `GoalController`, `Views/Goal/`, `GoalViewModels.cs`, `wwwroot/js/goal-planning.js` | Xem kế hoạch, phân tích khi tạo, xem trước, tạo mục tiêu, xác nhận/giữ mức sau đánh giá và kéo dài thời hạn. |
| `/WeightLog`, `/Profile` | `WeightLogController`, `ProfileController`, view và ViewModel cùng tên | Ghi hoặc xóa mốc cân, sửa hồ sơ và mức vận động. Cân nặng/hồ sơ mới có thể làm đổi TDEE và ngân sách ngày. |
| `/Food`, `/Food/Details/{id}`, `/Food/Favorites` | `FoodController`, `Views/Food/`, `FoodViewModels.cs` | Tra cứu món và dinh dưỡng, tìm kiếm JSON, lưu món yêu thích. |
| `/MealLog` | `MealLogController`, `Views/MealLog/`, `MealLogViewModels.cs` | Xem theo ngày, kiểm tra dị ứng, thêm món, đổi khẩu phần hoặc xóa chi tiết. Lưu snapshot dinh dưỡng theo khẩu phần. |
| `/MealPlan` | `MealPlanController`, `MealPlannerService`, `Views/MealPlan/` | Sinh thực đơn, tìm món thay thế, lưu thực đơn cá nhân, áp vào hôm nay và xóa bản đã lưu. |
| `/WaterExercise` | `WaterExerciseController`, `Views/WaterExercise/` | Ghi/xóa nước và vận động theo ngày, chỉnh mục tiêu nước. |
| `/Chat` | `ChatController`, `Views/Chat/`, `GeminiChatService` và các service ngữ cảnh | Tư vấn dinh dưỡng, lưu phiên và tin nhắn, tạo/xóa phiên và xóa lịch sử. |
| `/Admin`, `/Admin/SystemSelfTest` | `Areas/Admin/Controllers/`, `Areas/Admin/Views/`, `_AdminLayout.cshtml` | Quản trị theo vai trò `Admin`. Có Dashboard, Users, Foods, Categories, MealPlans, Articles và trang self-test. |

`Program.cs` cấu hình DI, SQL Server, Identity, routing MVC/Area, chống CSRF và seeding. `Filters/RequireOnboardingAttribute.cs` bảo vệ các luồng cần hồ sơ. Layout người dùng ở `Views/Shared/_Layout.cshtml`, CSS dùng chung ở `wwwroot/css/site.css`. Dashboard hiện chứa markup và JavaScript riêng trong Razor view. Admin có layout riêng.

## 3. Dữ liệu và những hợp đồng cần giữ

- `Data/AppDbContext.cs` là bản đồ EF. `ApplicationUser` liên kết với `UserProfile`, `Goal`, `WeightLog`, `MealLog`, `WaterLog`, `ExerciseLog`, `Favorite`, `ChatSession` và các đối tượng do người dùng sở hữu.
- `FoodCategory` → `Food`; `MealLog` → `MealLogDetail`; `MealPlan` → `MealPlanItem`; `ChatSession` → `ChatMessage`. `Article` cung cấp bài viết công khai và ngữ cảnh cho chatbot. `Exercise` là danh mục; `ExerciseLog` là lần ghi cụ thể.
- `MealLogDetail` giữ dinh dưỡng đã tính tại lúc ghi. Giá trị tại một khẩu phần được tính bằng `Food.<giá trị> × (Servings / ServingSize)`, rồi làm tròn như controller. Khi đọc báo cáo cũ, dùng snapshot đã lưu. Sửa món trong danh mục không phải lý do để tính lại nhật ký lịch sử.
- Cân nặng hồ sơ, cân nặng mục tiêu và log cân lưu một chữ số thập phân. `WeightPrecisionAttribute` kiểm tra đầu vào liên quan. Calo mục tiêu lưu và hiển thị dạng số nguyên với `GoalPolicy.RoundCalories` dùng `AwayFromZero`.
- `Goal.CaloriePlanJson` là cột nullable chứa `CaloriePlanState` có phiên bản, mức điều chỉnh đã đồng ý, trạng thái làm quen, mốc đánh giá, lý do và lịch sử đổi mức. Với bản ghi cũ thiếu JSON, `CalorieBudgetService.StateFor` ước tính từ `WeeklyRateKg` và gắn giải thích kế hoạch cũ. Mức ước tính vẫn phải qua kiểm tra giới hạn hiện tại.
- `Data/DbSeeder.cs` tạo vai trò, tài khoản mẫu, danh mục, món ăn, bài viết, vận động và thực đơn mẫu. Đây là seed khởi động, khác với dữ liệu demo `test2` được áp dụng riêng lên một database local.

### Bảo mật và quyền riêng tư

- Controller phải lấy `userId` từ `ClaimTypes.NameIdentifier`. Mọi thao tác đọc/sửa/xóa theo ID bản ghi của người dùng phải lọc cả ID và chủ sở hữu. Dữ liệu gửi từ client chỉ là đầu vào cần xác thực.
- Duy trì antiforgery cho POST. AJAX có thể gửi trường `__RequestVerificationToken` hoặc dùng header `RequestVerificationToken` theo cấu hình hiện tại. Quick Add Water của Dashboard gửi token qua `FormData`, kèm `X-Requested-With: XMLHttpRequest` để nhận JSON.
- Vai trò `Admin` quản lý nội dung và thống kê. Không mở thêm đường truy cập đến hồ sơ sức khỏe, nhật ký riêng hay lịch sử chat của người dùng. Các test hiện có kiểm tra cả chống IDOR và phạm vi self-test của Admin.
- Truyền cấu hình API và SMTP qua môi trường hoặc kho bí mật của môi trường chạy. Khi bàn giao cho AI khác, chỉ gửi các tên khóa cấu hình. Giữ dữ liệu người dùng, secret và các bản sao database ngoài nội dung trao đổi.

## 4. Nghiệp vụ cân nặng và calo

| Thành phần | Vai trò |
| --- | --- |
| `NutritionCalculatorService` | Tính tuổi theo ngày Việt Nam, BMI, BMR, TDEE và macro khuyến nghị. |
| `GoalPlanningService` và `GoalPolicy` | Kiểm tra hồ sơ, chiều tăng/giảm, giới hạn, thời hạn, mức điều chỉnh ban đầu và ước tính theo giai đoạn. `CanApply` kiểm tra lại mức năng lượng và BMI. `GoalPolicy` cung cấp ngày nghiệp vụ Việt Nam và công thức calo còn lại. |
| `CalorieBudgetService` | Tính ngân sách hiệu lực từ hồ sơ và mục tiêu. Nguồn dùng chung cho Dashboard, MealLog, MealPlan, Profile và ngữ cảnh chatbot. `RefreshAsync` đồng bộ ngân sách lưu khi hồ sơ/cân nặng đổi. |
| `AdaptiveCalorieService` | Đọc dữ liệu cân, ăn và vận động của tối đa 28 ngày, đánh giá chu kỳ và tạo đề xuất. Kết quả có fingerprint để phát hiện dữ liệu đã đổi từ lúc hiển thị. |
| `GoalController` | Kiểm tra và lưu kế hoạch phía server. Các thao tác tạo/thay thế và đánh giá mục tiêu dùng transaction; chỉ áp dụng đề xuất sau khi kiểm tra quyền sở hữu, fingerprint, xác nhận và giới hạn. |

Các phép tính dưới đây cần giữ thống nhất giữa các màn hình.

```text
BMI = cân nặng kg / (chiều cao cm / 100)^2
BMR = 10 × kg + 6,25 × cm − 5 × tuổi + hằng số giới tính
TDEE = BMR × hệ số vận động
Điều chỉnh cần theo thời hạn = |cân hiện tại − cân đích| × 7.700 / số ngày
Ngân sách giảm/tăng = TDEE −/+ mức điều chỉnh đang áp dụng
Ngân sách duy trì = TDEE
Calo vận động được cộng lại = làm tròn(min(300, max(0, calo vận động đã ghi) × 0,5))
Calo còn lại = ngân sách − calo đã nạp + calo vận động được cộng lại
Calo tiêu hao của bản ghi vận động mới = max(MET − 1, 0) × 3,5 × cân nặng kg / 200 × số phút
```

- Hằng số BMR theo thứ tự nam, nữ và khác là `+5`, `−161`, `−78`. Giá trị `−78` là quy ước đồ án. Hệ số vận động từ `1,2` đến `1,9` theo `ActivityLevel`. Các phép tính năng lượng và thời hạn là ước tính của ứng dụng.
- Phần calo vận động được cộng lại bằng 50% tổng tiêu hao đã ghi trong ngày, làm tròn theo `GoalPolicy.RoundCalories` và giới hạn 300 kcal. Dashboard, nhật ký ăn, công cụ chatbot và đánh giá nhật ký ăn sử dụng chung chính sách này. Ví dụ ngân sách 1.800 kcal, đã nạp 1.500 kcal và ghi 400 kcal vận động thì còn 500 kcal. Với 800 kcal vận động, còn 600 kcal. Bản ghi mới tính tiêu hao trên mức nghỉ, còn lịch sử dùng nguyên giá trị đã lưu.
- `GoalPolicy` áp dụng trần thâm hụt theo ba bậc BMI, sàn calo và tầng sàng lọc. Mức tham chiếu giảm cân là 550 kcal/ngày trong giới hạn hồ sơ. Tốc độ trên 0,75 kg/tuần cần xác nhận trước khi lưu. Giai đoạn làm quen tối đa 250 kcal/ngày.
- Mục tiêu giảm cân đang hoạt động có phản hồi cảm nhận trên trang Mục tiêu và Dashboard. Nhắc sau bảy ngày từ lần phản hồi hoặc điều chỉnh gần nhất. Tăng hoặc giảm dùng bước 175 kcal/ngày, giới hạn bởi hồ sơ và mức thâm hụt tối thiểu bằng 0. App tính lại kế hoạch khi xác nhận bản xem trước. Mốc phản hồi lưu trong `CaloriePlanJson`. Chu kỳ đánh giá xu hướng 14 ngày chạy riêng.
- Phân loại BMI người lớn trong ứng dụng là dưới 18,5 thiếu cân, 18,5 đến dưới 23 bình thường, 23 đến dưới 25 thừa cân và từ 25 béo phì. Khi tăng cân, BMI hiện tại phải dưới 25 và BMI đích tối đa 24,9. Các ngưỡng tăng cân, sàn và trần năng lượng là quy tắc sản phẩm. Tham chiếu cho phân loại BMI nằm ở Bảng 4.1. [Hướng dẫn của Bộ Y tế theo Quyết định 2892/QĐ-BYT](https://emohbackup.moh.gov.vn/publish/attach/getfile/409620)
- Khi tạo mục tiêu giảm hoặc tăng cân, chênh lệch tối thiểu là 1,0 kg và thời hạn tối thiểu 2 tuần. Với chênh lệch nhỏ, giao diện hướng người dùng sang mục tiêu duy trì. `ScopeRestriction` kiểm tra tuổi, tính hợp lệ của hồ sơ và kết quả sàng lọc có cấu trúc.
- **Ngày mong muốn** (`Goal.TargetDate`) và **ngày dự kiến** là hai giá trị khác nhau. Thay đổi cân nặng/hồ sơ có thể làm đổi TDEE và ngày dự kiến. Khi phù hợp, vẫn giữ mức điều chỉnh đã xác nhận. Nếu không còn phù hợp, dùng TDEE và giải thích việc tạm ngừng điều chỉnh. Gia hạn `TargetDate` là thao tác riêng.
- Đến kỳ 14 ngày, gộp nhiều lần cân trong một ngày thành trung bình ngày. Cần ít nhất hai ngày cân mỗi nửa kỳ và khoảng quan sát ít nhất 10 ngày. Xu hướng chính so sánh trung bình hai nửa theo khoảng cách ngày trung bình. Tốc độ từ mốc đầu/cuối là số riêng. Thiếu dữ liệu thì yêu cầu ghi thêm. Ngưỡng quá nhanh là `min(1 kg, 1% × cân trung bình nửa cuối)` mỗi tuần khi giảm và `min(0,5 kg, 0,75% × cân trung bình nửa cuối)` mỗi tuần khi tăng. Xem xét tăng mức cho xu hướng chậm cần hai kỳ, độ phủ nhật ký ăn và các điều kiện trong `AdaptiveCalorieService`. Đề xuất được giới hạn theo hồ sơ hiện tại.
- Khi đạt đích theo xu hướng, hệ thống **đề xuất** chuyển sang duy trì. Người dùng chọn áp dụng, giữ nguyên hoặc kéo dài. Kế hoạch cũ vượt trần hoặc giới hạn BMI mới hiển thị trạng thái tạm ngừng điều chỉnh và ngân sách theo TDEE, với lý do từ `CanApply`.
- `GoalPolicy.Today` và `GoalPolicy.Now` dùng giờ Việt Nam cho ngày nghiệp vụ. Đồng hồ thử nghiệm kiểm tra mốc 17:00 UTC, tương ứng 00:00 tại Việt Nam. Các mốc kỹ thuật như `CreatedAt` và `UpdatedAt` tiếp tục lưu UTC. Truy vấn Admin theo ngày và tháng Việt Nam chuyển ranh giới sang UTC khi lọc các mốc kỹ thuật đó.

## 5. Gợi ý món và chatbot

- `MealPlannerService` dùng ngân sách từ service chung. Phân bổ bốn bữa mặc định theo sáng 25%, trưa 35%, tối 30% và phụ phần còn lại khoảng 10%. Có nhánh cho số bữa khác. Giữ bộ lọc chế độ ăn, dị ứng và món không thích khi sinh hoặc đổi món. Giữ dữ liệu thực đơn đã lưu và nhật ký cũ.
- `GeminiChatService` chạy `ChatSafetyFilter`, dựng ngữ cảnh qua `UserContextBuilder`, truy xuất bài viết qua `RagService` và chọn truy vấn CSDL qua `FoodToolService` trước khi gửi prompt. Với câu hỏi có rủi ro, giữ cảnh báo, xử lý an toàn và lời lưu ý y tế. Chatbot chỉ giải thích ngân sách và đề xuất từ service, không thay đổi mục tiêu.
- `FoodToolService` và `UserContextBuilder` xác định ngày mặc định theo `GoalPolicy.Today` và tính phần còn lại qua `GoalPolicy.RemainingCalories`. Ngữ cảnh và phản hồi dự phòng phân biệt lượng vận động đã ghi với `ExerciseCreditApplied`, tức phần thực sự được cộng lại vào ngân sách.
- README dùng từ **Function Calling** và mô tả tìm kiếm ngữ nghĩa. Mã hiện tại thực thi **định tuyến công cụ trong C# trước khi gọi Gemini**. Payload `generateContent` có system instruction, nội dung hội thoại và generation config. `RagService` chấm điểm từ khóa trên bài viết đã công bố. Phần vector/cosine hiện là hướng mở rộng. Nên mô tả theo hành vi này khi demo kỹ thuật.
- Cấu hình `Gemini:ApiKey`, `Gemini:Model` với mặc định trong code là `gemini-2.5-flash`. Thiếu key hoặc API lỗi thì dùng fallback dựa trên dữ liệu CSDL. SMTP cho luồng đặt lại mật khẩu đọc `SmtpSettings` với các khóa `Host`, `Port`, `EnableSsl`, `UserName`, `Password`, `SenderEmail`, `SenderName`.

## 6. Chạy và kiểm thử

### Chạy ứng dụng thật

Mở PowerShell trên Windows. Cần runtime ASP.NET Core 8 và SDK có thể build `net8.0`. Dùng LocalDB của đúng tài khoản Windows chứa dữ liệu. Kết nối được đọc từ `ConnectionStrings:DefaultConnection`, tên database mặc định là `NutriTrackAIDb`. Dùng cùng môi trường chạy khi kiểm tra cấu hình và áp dụng migration.

```powershell
Set-Location -LiteralPath 'D:\DACN\NutriTrackAI'
dotnet restore '.\NutriTrackAI.csproj'
dotnet run --project '.\NutriTrackAI.csproj' --launch-profile http
```

Profile `http` dùng `http://localhost:5127`. Profile `https` dùng `https://localhost:7212` và `http://localhost:5127`. Cả hai là `Development`. Sau khi ứng dụng chạy, mở `/Account/Login`, đăng nhập bằng tài khoản thử được phép dùng, rồi vào `/Dashboard` hoặc `/Goal`.

### Migration và LocalDB

- Các migration hiện có là `20261026094736_InitialCreate`, `20261107105859_AddAdaptiveCaloriePlan` và `20261110053547_AddHealthScreening`. Hai migration bổ sung lần lượt thêm `goals.CaloriePlanJson` và `user_profiles.HealthScreeningJson`. [Script SQL bổ sung kế hoạch](AddAdaptiveCaloriePlan.sql) · [Chi tiết sàng lọc](HEALTH_SCREENING.md)
- Với database trống hoặc còn migration khác, xác nhận đúng database đích rồi chạy `dotnet ef database update --project '.\NutriTrackAI.csproj' --startup-project '.\NutriTrackAI.csproj' -- --environment Development`. Dùng `dotnet-ef` tương thích EF Core 8. Chỉ trường hợp duy nhất còn pending migration `AddAdaptiveCaloriePlan` được ứng dụng tự cập nhật trong Development trước khi seed.
- `Data/LocalDbDevelopmentConnection.cs` xử lý Windows + Development + kết nối `(localdb)`. Nó dùng `SqlLocalDB info`, khởi động instance nếu thiếu pipe, đọc lại pipe và giữ database/xác thực của cấu hình gốc. Với lỗi registry, kiểm tra từ PowerShell chạy cùng tài khoản Windows với Visual Studio và đối chiếu nhật ký Windows. Mỗi lần instance khởi động có thể đổi named pipe. [Hồ sơ xử lý LocalDB](LOCALDB_STARTUP_FIX.md).
- Khi chỉ làm UI, ưu tiên host QA dưới đây để tránh chạy seeder/migration và tránh ghi vào demo thật. Khi phải thử ghi thật, dùng tài khoản thử riêng và ghi rõ dữ liệu đã tạo.

### Build, test và host QA cách ly

```powershell
Set-Location -LiteralPath 'D:\DACN\NutriTrackAI'
dotnet restore '.\NutriTrackAI.Tests\NutriTrackAI.Tests.csproj'
dotnet build '.\NutriTrackAI.csproj' --no-restore -p:UseSharedCompilation=false
dotnet test '.\NutriTrackAI.Tests\NutriTrackAI.Tests.csproj' --no-restore -p:UseSharedCompilation=false
dotnet run --project '.\NutriTrackAI.Tests\NutriTrackAI.Tests.csproj' --no-build -- --serve
```

- Host QA của `NutriTrackAI.Tests/QaApplication.cs` dùng EF InMemory và dữ liệu giả lập tại `http://127.0.0.1:5168`. Mở `/qa/signin` để đăng nhập nhanh, sau đó mở `/Dashboard`, `/Goal` hoặc `/Goal/Create`. Route QA này chỉ có trong host thử nghiệm với `--serve`. Dừng bằng `Ctrl+C`.
- `PlanningTests.cs` kiểm tra công thức, giới hạn, kế hoạch cũ, dữ liệu thiếu và thuật toán xu hướng. `WorkflowTests.cs` kiểm tra các luồng MVC, onboarding, antiforgery, IDOR, xác nhận và dùng chung ngân sách. `VietnamBusinessDateTests.cs` kiểm tra mốc nửa đêm và ngày được dùng khi đọc dữ liệu hoặc ghi nhanh nước. `ExerciseCreditTests.cs` kiểm tra làm tròn, trần phần vận động được cộng lại và đối chiếu nhật ký ăn khi đánh giá. `LocalDbConnectionTests.cs` kiểm tra resolver theo các tình huống giả lập.
- Build và test bàn giao ngày 08/11/2026 dùng SDK `10.0.401`, runtime ASP.NET Core 8 có sẵn. Build **0 cảnh báo, 0 lỗi**. Test **82 đạt, 0 lỗi, 0 bỏ qua**. Phạm vi này không thay cho kiểm thử trên database LocalDB thật hoặc gọi Gemini/SMTP thật.

## 7. Dữ liệu demo hiện có theo hồ sơ kiểm tra

Các bản ghi sau là dữ liệu mô phỏng đã được kiểm tra trong các phiên trước. Chúng nằm trong database local của phiên làm demo. Một bản sao mã nguồn hoặc một database mới sẽ không tự có các bản ghi này.

- **Demo mới nhất của `test2`** đi từ 72,0 đến 70,0 kg trong lộ trình 42 ngày, kết thúc 07/11/2026. Hồ sơ demo đã xác minh 43 mốc cân gồm mốc bắt đầu, 42 ngày nhật ký ăn, 168 bữa, tiến độ 100% và mức điều chỉnh đã xác nhận khoảng 366,67 kcal/ngày. Ngân sách ngày cuối 2.187 kcal; chuyển duy trì sẽ khoảng 2.554 kcal nếu người dùng xác nhận. [Kịch bản và kết quả demo 42 ngày](DEMO_TEST2_COMPLETE_42_DAYS.md).
- Tài liệu **14 ngày** là trạng thái lịch sử trước khi mở rộng demo. Không trộn các số 71,4 kg / 2.209 kcal của mốc 14 ngày với kết quả hoàn thành. Chọn đúng ngày trong `/MealLog` khi cần xem bốn bữa của demo. Số hôm nay trên Dashboard phụ thuộc ngày hiện tại. [Kịch bản demo 14 ngày](DEMO_TEST2_14_DAYS.md).
- Dấu vết kiểm chứng ở `.qa/test2-complete42/verified-final.json` và backup trước thay đổi ở `.qa/test2-complete42/before.json`. Có bộ QA riêng cho xu hướng cân dùng trong test; nó khác với `test2` trên LocalDB. [Kiểm toán dữ liệu QA](QA_WEIGHT_AUDIT.md).

## 8. Bàn giao công việc giao diện

Dashboard trong mã hiện tại đã có khối calo còn lại, khu vực nước với nút ghi nhanh, khu vực mục tiêu cân nặng và biểu đồ. Đợt nghiệp vụ 09/11 chỉ cập nhật ngày và nội dung giải thích các chỉ số trong bố cục này. Báo cáo Impeccable **24/40** là đánh giá lịch sử trước khi Dashboard được sắp xếp lại. [Báo cáo Impeccable](../.impeccable/critique/2026-11-08T16-04-15Z__views-dashboard-index-cshtml.md)

- Mọi số liệu năng lượng lấy từ ViewModel. Hiển thị riêng calo vận động đã ghi và phần được cộng lại. Khi calo còn lại âm, làm rõ mức vượt.
- Nút +250 ml dùng thao tác POST hiện có và các trường `totalWater`, `dailyGoal`, `percent` trả về. Nếu đổi markup, cập nhật các ID mà JavaScript dùng là `dashTotalWater`, `dashWaterBadge` và `dashWaterBar` cùng lúc.
- Khi tiếp tục công việc giao diện, dùng host QA để thử ở **1440 px và 390 px**, kiểm tra số liệu, ghi nhanh nước, trạng thái trống, focus, tương phản và tràn ngang. Báo rõ môi trường xác minh.

## 9. Các giới hạn cần mang sang phiên mới

- Ngân sách là ước tính và dữ liệu ăn/vận động do người dùng tự ghi. Chu kỳ đánh giá được tính khi luồng cần đọc đánh giá, hiện chưa có job nền hay email nhắc riêng.
- Các hướng còn để ngỏ gồm ước tính TDEE thực từ nhật ký, dùng cân xu hướng để lập kế hoạch và chuyển duy trì theo giai đoạn. Xem các phần chưa làm ở mục Triển khai đợt 3.
- Migration tự động có phạm vi hẹp trong Development. Phiên này chưa tái xác nhận instance, schema hoặc dữ liệu demo trên SQL Server. Cần kiểm tra đúng môi trường nếu tiếp tục xử lý LocalDB.
- Tài liệu mô tả tính năng có thể cũ hơn mã. Khi có khác biệt, đối chiếu service/controller, migration và test trước khi sửa. Không suy từ tên công nghệ trong README rằng tính năng tương ứng đã có đầy đủ.
- Môi trường bàn giao chưa có Git khả dụng. Nếu AI nhận một clone khác, bắt đầu bằng `git status --short --branch` và so sánh các file trước khi chỉnh. Nếu nhận bản copy, dùng chính thư mục dự án và giữ các thay đổi hiện có.

## 10. Tài liệu đọc tiếp

| Nguồn | Đọc khi |
| --- | --- |
| [README](../README.md) | Cần tổng quan, cài đặt, các kịch bản nghiệm thu và vai trò. |
| [Ngữ cảnh nghiệp vụ](../gemini.md) | Cần hiểu yêu cầu ban đầu về dữ liệu, công thức, bảo mật và trợ lý. Đối chiếu phần mục tiêu và chatbot với mã hiện tại. |
| [Báo cáo nâng cấp mục tiêu](WEIGHT_GOAL_UPGRADE.md) | Cần biết lý do thay đổi, công thức chi tiết, file sửa và các tình huống nghiệm thu. |
| [Kiểm tra dữ liệu QA](QA_WEIGHT_AUDIT.md) | Cần bộ số liệu và ví dụ tái lập cách tính xu hướng. |
| [LocalDB](LOCALDB_STARTUP_FIX.md) | Ứng dụng vướng khởi động SQL Server hoặc lỗi migration. |
| [Demo 42 ngày](DEMO_TEST2_COMPLETE_42_DAYS.md) | Cần trình bày trọn luồng quản lý cân nặng, thực đơn và chuyển duy trì. |

## 11. Báo cáo triển khai nghiệp vụ ngày 09/11/2026

| Nhóm | Build | Test đạt | Kiểm tra chính |
| --- | --- | ---: | --- |
| A. Ngày Việt Nam | 0 lỗi, 0 cảnh báo | 86/86 | 4 ca mới cho mốc 16:59, 17:00 và 18:00 UTC, đọc cùng ngày ở Dashboard, ăn, nước, công cụ chatbot và ghi nhanh nước đúng ngày. |
| B. Calo vận động | 0 lỗi, 0 cảnh báo | 94/94 | 8 ca mới cho mức âm, làm tròn, trần 300, đối chiếu nhật ký ăn, lưu MET thuần và giữ bản ghi cũ. Mở rộng test ngân sách dùng chung với hai log vận động tổng 400 kcal. |
| C. Trần theo hồ sơ | 0 lỗi, 0 cảnh báo | 101/101 | 7 ca mới cho các nhánh giới hạn, sàn, thời hạn, xu hướng tăng nhanh, trần đề xuất và tạm dừng kế hoạch cũ. Các ca đồng ý điều chỉnh chuyển từ 650/700 sang 600/630 vì hai giá trị cũ vượt trần của hồ sơ test. Ca 11 tuần cho mục tiêu giảm 7 kg nay bị chặn. Ca giảm nhanh dùng tốc độ nằm giữa ngưỡng mới và ngưỡng cố định cũ. |
| D. BMI và mục tiêu tối thiểu | 0 lỗi, 0 cảnh báo | 113/113 | 12 ca mới cho mốc BMI, giới hạn tăng cân, mục tiêu dưới 1 kg hoặc 2 tuần, kế hoạch cũ và thông báo preview. Ca tích hợp Duy trì dùng 1 tuần để xác nhận giới hạn mới chỉ áp dụng khi tạo mục tiêu tăng/giảm. |

Khi ngân sách là 1.800 kcal và đã nạp 1.500 kcal, ghi 400 kcal vận động cho kết quả **500 kcal còn lại** thay cho 700. Ghi 800 kcal cho kết quả **600 kcal còn lại** thay cho 1.100. Hồ sơ nam 24 tuổi, 72 kg, 170 cm, TDEE 2.584,625 có trần **646,16 kcal/ngày** khi làm tròn hiển thị. Với ví dụ nam 85 → 75 kg, thời hạn 9 tuần bị chặn, tối thiểu tính toán đổi từ 15 lên **19 tuần**, và thời hạn đề xuất theo mức thông thường là **22 tuần**.

Các luồng tích hợp dùng HTTP với EF InMemory. Test chính sách và các ca múi giờ dùng dữ liệu giả lập. Không chạy migration, ghi LocalDB, chỉnh dữ liệu `test2` hay gọi Gemini/SMTP thật trong đợt này. Mã chỉ thay đổi cách hiển thị phần calo còn lại khi đọc lịch sử và cách tính tiêu hao cho bản ghi vận động mới. Các giá trị đã lưu giữ nguyên. Sàng lọc có cấu trúc, ước tính TDEE từ nhật ký, chuyển sang duy trì theo từng bước và dùng cân trung bình bảy ngày làm đầu vào kế hoạch nằm ngoài đợt này.

### Danh mục file sửa và thêm

- Chính sách và dữ liệu: `Services/GoalPlanningService.cs`, `Services/CalorieBudgetService.cs`, `Services/AdaptiveCalorieService.cs`, `Services/NutritionCalculatorService.cs`, `Data/DbSeeder.cs`.
- Thực đơn và chatbot: `Services/MealPlannerService.cs`, `Services/FoodToolService.cs`, `Services/IFoodToolService.cs`, `Services/UserContextBuilder.cs`, `Services/IUserContextBuilder.cs`, `Services/GeminiChatService.cs`.
- Controller: `Controllers/DashboardController.cs`, `Controllers/GoalController.cs`, `Controllers/MealLogController.cs`, `Controllers/MealPlanController.cs`, `Controllers/OnboardingController.cs`, `Controllers/WaterExerciseController.cs`, `Controllers/WeightLogController.cs`, `Areas/Admin/Controllers/DashboardController.cs`, `Areas/Admin/Controllers/SystemSelfTestController.cs`.
- ViewModel: `Models/ViewModels/DashboardViewModels.cs`, `Models/ViewModels/FoodViewModels.cs`, `Models/ViewModels/MealLogViewModels.cs`, `Models/ViewModels/OnboardingViewModel.cs`, `Models/ViewModels/RegisterViewModel.cs`, `Models/ViewModels/WaterExerciseViewModels.cs`, `Models/ViewModels/WeightLogViewModels.cs`, `Areas/Admin/Models/SystemSelfTestViewModel.cs`.
- Razor và JavaScript: `Views/_ViewImports.cshtml`, `Views/Dashboard/Index.cshtml`, `Views/Food/Details.cshtml`, `Views/Goal/Create.cshtml`, `Views/Goal/Index.cshtml`, `Views/Home/Index.cshtml`, `Views/MealLog/Index.cshtml`, `Views/Onboarding/Index.cshtml`, `Views/WaterExercise/Index.cshtml`, `Views/WeightLog/Index.cshtml`, `Areas/Admin/Views/_ViewImports.cshtml`, `Areas/Admin/Views/Shared/_AdminLayout.cshtml`, `wwwroot/js/goal-planning.js`.
- Kiểm thử và tài liệu: `NutriTrackAI.csproj` mở quyền truy cập nội bộ cho project test. Thêm `NutriTrackAI.Tests/VietnamBusinessDateTests.cs` và `NutriTrackAI.Tests/ExerciseCreditTests.cs`. Cập nhật `NutriTrackAI.Tests/PlanningTests.cs`, `NutriTrackAI.Tests/WorkflowTests.cs`, `README.md`, `gemini.md` và tài liệu này.

## Prompt mở phiên cho AI tiếp quản

```text
Bạn tiếp quản dự án NutriTrackAI tại D:\DACN\NutriTrackAI.
Đọc docs/AI_HANDOFF.md trước, sau đó đối chiếu phần liên quan với mã nguồn.
Nếu workspace khác máy cũ, xác định thư mục dự án và cơ sở dữ liệu của máy mới.

Thực hiện tác vụ tôi giao. Khi sửa nghiệp vụ, đọc mục 3 đến 6 và kiểm tra
các quy tắc tập trung trong GoalPolicy. Khi sửa giao diện, kiểm tra view,
ViewModel, controller, CSS và JavaScript hiện tại theo mục 8.
Dùng host QA InMemory để xác minh luồng và giữ dữ liệu demo LocalDB.
Chạy build và test, báo các file đã sửa, kết quả thực tế và giới hạn còn lại.
```
