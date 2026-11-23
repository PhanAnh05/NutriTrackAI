# Bàn giao đồ án NutriTrackAI cho cuộc trò chuyện mới

Cập nhật ngày **16/11/2026**. Khi mở cuộc trò chuyện mới trong cùng dự án, có thể gửi đường dẫn file này. Nếu công cụ mới chưa có quyền đọc thư mục, sao chép phần từ **Ngữ cảnh tiếp quản** đến cuối và gửi kèm mã nguồn cần thiết.

## Ngữ cảnh tiếp quản

Tôi đang làm đồ án **NutriTrackAI**, website tiếng Việt theo dõi cân nặng, ăn uống, nước uống và vận động, có lập mục tiêu calo, gợi ý thực đơn món Việt và trợ lý Gemini. Workspace hiện tại là `D:\DACN\NutriTrackAI` trên Windows. Đây là dự án đang chạy và đã qua ba nhóm sửa ở đợt 2. Hãy dùng thông tin dưới đây để hiểu hiện trạng trước khi thực hiện yêu cầu mới của tôi. Đối chiếu tài liệu với mã nguồn và kết quả kiểm thử gần nhất trước khi sửa. Giữ thay đổi đúng phần tôi giao.

## 1. Công nghệ và luồng sản phẩm

- **Máy chủ** dùng C#, ASP.NET Core MVC, `net8.0`, EF Core `8.0.33`, SQL Server/LocalDB. `Program.cs` cấu hình DI, Identity, routing, migration và seed. `Data/AppDbContext.cs` là bản đồ EF. `Data/DbSeeder.cs` chứa dữ liệu mẫu khởi động.
- **Giao diện** dùng Razor Views, Bootstrap, Bootstrap Icons, CSS dự án và JavaScript thuần. Chart.js vẽ biểu đồ. Giữ bố cục, nhãn tiếng Việt, responsive và trạng thái điều hướng khi sửa màn hình.
- **Luồng người dùng** gồm đăng ký/đăng nhập → Onboarding hồ sơ và sàng lọc → Dashboard → chọn hoặc theo dõi mục tiêu → ghi ăn, nước, vận động, cân nặng → xem tiến độ, thực đơn và hỏi trợ lý. Quản trị có khu vực riêng.
- **Identity** dùng cookie và khóa người dùng `int`. Người chưa hoàn tất hồ sơ được đưa tới Onboarding ở các trang có `RequireOnboarding`. Các thao tác dữ liệu cá nhân lấy người dùng từ Claims, lọc theo chủ sở hữu và giữ antiforgery cho POST. Admin quản lý nội dung và xem thống kê tổng hợp, dữ liệu nhật ký, hội thoại và sàng lọc cá nhân vẫn theo quyền sở hữu.

## 2. Bản đồ để tìm đúng mã

| Tác vụ | Điểm vào và nơi triển khai |
| --- | --- |
| Trang chủ, đăng nhập, điều hướng | `/`, `Controllers/AccountController.cs`, `Views/Home/Index.cshtml`, `Views/Shared/_Layout.cshtml`, `wwwroot/css/site.css`. Trang chủ đổi nút theo trạng thái đăng nhập và vai trò. Thanh điều hướng dùng route của MVC để đánh dấu trang hoặc nhóm đang mở. `Goal` thuộc mục **Cân nặng & Mục tiêu**. |
| Hồ sơ và sàng lọc | `/Onboarding`, `/Profile`, `OnboardingController`, `ProfileController`, `Models/Entities/HealthScreening.cs`. |
| Tạo và theo dõi mục tiêu | `/Goal`, `/Goal/Create`, `GoalController`, `Services/GoalPlanningService.cs`, `Services/CalorieBudgetService.cs`, `Services/AdaptiveCalorieService.cs`. Xem thêm `Views/Goal`, `Views/Shared/_PaceChoices.cshtml`, `wwwroot/js/pace-picker.js` và `wwwroot/js/goal-planning.js`. |
| Cân nặng và Dashboard | `/WeightLog`, `/Dashboard`, `WeightLogController`, `DashboardController`, `Services/WeightTrackingService.cs`, `Views/Shared/_WeightMismatchBanner.cshtml`. Hai POST đối chiếu là `/WeightLog/ReconcileUseProfile` và `/WeightLog/ReconcileUseLog`. |
| Nhật ký, món ăn và thực đơn | `/MealLog`, `/Food`, `/MealPlan`, `/WaterExercise`, các controller/view cùng tên và `Services/MealPlannerService.cs`. |
| Chatbot và quản trị | `/Chat`, `ChatController`, `GeminiChatService`, `ChatSafetyFilter`, `RagService`, `FoodToolService`, `UserContextBuilder`. Khu vực `/Admin` nằm trong `Areas/Admin/`. |
| Kiểm thử | `NutriTrackAI.Tests/PlanningTests.cs` kiểm công thức/chính sách, `WorkflowTests.cs` kiểm luồng HTTP/Razor, `VietnamBusinessDateTests.cs` kiểm ngày Việt Nam, `ExerciseCreditTests.cs` kiểm phần calo vận động được cộng lại. `QaApplication.cs` dựng host và dữ liệu giả lập. |

## 3. Quy tắc nghiệp vụ hiện có

### Cân nặng, mục tiêu và calo

- `GoalPolicy.Today` và `GoalPolicy.Now` dùng giờ Việt Nam. `17:00 UTC` là `00:00` ngày kế tiếp tại Việt Nam. Giá trị kỹ thuật như `CreatedAt`, `UpdatedAt` lưu UTC. Cân nặng lưu đến một chữ số thập phân.
- `WeightTrackingService` dùng chung cho Hồ sơ, Nhật ký và Onboarding. Chọn bản ghi mới nhất theo ngày rồi ID giảm dần. Đổi cân ở Hồ sơ có thể tạo/cập nhật mốc hôm nay. Sửa hoặc xóa mốc cuối phải xác định lại cân hiện tại. Nếu dữ liệu cũ lệch, ba trang Dashboard, Hồ sơ, Cân nặng cho chọn dùng cân Hồ sơ hoặc mốc gần nhất. Khi chưa đối chiếu, tiến độ/biểu đồ đọc Nhật ký, ngân sách calo vẫn đọc cân Hồ sơ. Lưu trường khác không tự ghi cân khi hai số đang lệch. Chênh lệch **lớn hơn** `max(2,0 kg, 3% × cân tham chiếu)` trong `0–14` ngày cần xác nhận trong hai biểu mẫu Hồ sơ và Nhật ký.
- `NutritionCalculatorService` tính BMI, BMR theo Mifflin–St Jeor, TDEE và macro. Phân loại BMI người lớn trong app dùng các mốc `18,5 / 23 / 25`. Giảm hoặc tăng cần chênh ít nhất `1 kg` và tối thiểu `2 tuần`. Duy trì dùng mức điều chỉnh `0`.
- Trần thâm hụt của ứng dụng là `min(750, 25% × TDEE, 11 × cân nặng kg, max(0, TDEE − sàn))`. Sàn giảm cân là `1.200 kcal/ngày` cho nữ và `1.500` cho nam/khác. Trần thặng dư là `min(500, 20% × TDEE)`. Kiểm tra giới hạn và BMI ở máy chủ. Đây là chính sách của ứng dụng và các con số dự kiến là ước tính.
- Mức cần theo thời hạn được tính theo `abs(cân hiện tại − cân đích) × 7.700 ÷ số ngày`. Tốc độ theo thời hạn dùng chênh lệch cân chia số tuần nguyên. Khi hợp lệ, kế hoạch mới áp dụng đúng mức cần theo thời hạn, với tùy chọn làm quen tối đa `250 kcal/ngày` trên trang Tạo mục tiêu. Cả Tạo mục tiêu và Onboarding yêu cầu xác nhận nếu giảm trên `0,5 kg/tuần` trong giới hạn. Máy chủ tính ba lựa chọn nhanh và gửi số tuần cùng tốc độ tương ứng. Người dùng vẫn có thể nhập tuần thủ công. JavaScript không tự ghi đè số tuần khi đổi đích hay hồ sơ. `/Goal/Preview` trả HTML partial và `/Onboarding/CalculatePreview` trả JSON.
- `CalorieBudgetService` là nguồn chung cho Dashboard, Nhật ký ăn, Mục tiêu, Thực đơn và chatbot. Calo còn lại = ngân sách hiệu lực − đã nạp + phần vận động được cộng. Phần cộng lại bằng `50%` calo vận động đã ghi, giới hạn `300 kcal/ngày`, làm tròn theo `GoalPolicy`. Vận động mới tính phần tiêu hao trên mức nghỉ theo `max(MET − 1, 0)`. Giữ giá trị đã lưu của bản ghi cũ. Giao diện dùng **Ngân sách calo** khi có kế hoạch hiệu lực và **Mốc duy trì (TDEE)** khi tạm ngừng hoặc chưa có mục tiêu.
- `Goal.CaloriePlanJson` lưu mức điều chỉnh đã xác nhận và lịch sử. Mục tiêu cũ thiếu JSON có nhánh tương thích. Dữ liệu cân mới có thể đổi TDEE và ngày dự kiến, mức điều chỉnh đã xác nhận chỉ giữ khi còn hợp lệ. Phân biệt ngày mong muốn và ngày dự kiến. Đánh giá 14 ngày so xu hướng hai nửa kỳ, cần đủ mốc cân, đối chiếu nhật ký ăn khi đề xuất tăng mức. Phản hồi cảm nhận 7 ngày là luồng riêng. Đề xuất thay đổi hoặc chuyển duy trì cần xác nhận. Nếu sửa luồng thay đổi ngày hay hoàn tất, đọc `GoalController.Review`, `ConfirmBodyAdjustment`, `Extend` và mô hình `Goal` trước khi giả định cách lưu lịch sử.

### Sàng lọc, nhật ký và trợ lý

- **Ứng dụng hiện tại có 4 câu sàng lọc có cấu trúc**, dạng `Có`, `Không`, `Chưa chắc chắn` hoặc chưa trả lời. Máy chủ suy trạng thái từ câu trả lời lưu trong `UserProfile.HealthScreeningJson`. `HealthNotes` là ghi chú, không phải điều kiện quyết định. Hồ sơ hạn chế, chưa khai đủ hoặc dưới 18 tuổi có thể vào nhật ký theo chế độ theo dõi. Khi đã tạm ngừng do sàng lọc, hoàn tất lại khai báo không tự kích hoạt kế hoạch cũ. Dashboard giải thích lý do, dừng gợi ý món cá nhân hóa và hiển thị TDEE tham khảo. Chatbot ở trạng thái này trả tóm tắt nhật ký.
- `MealLogDetail` lưu snapshot dinh dưỡng theo khẩu phần lúc ghi. Bảo toàn snapshot khi đọc dữ liệu lịch sử. Khi gợi ý hoặc đổi món, tiếp tục áp dụng kiểm tra dị ứng, món không thích và chế độ ăn. Bốn bữa mặc định của luồng thực đơn phân bổ `25/35/30/10`.
- Chatbot gọi `ChatSafetyFilter` trước, dùng ngữ cảnh và truy vấn CSDL từ C#, truy xuất bài viết công bố theo từ khóa rồi gọi Gemini. Khi thiếu khóa hoặc API lỗi có fallback. Giải thích theo dữ liệu, giữ chế độ chỉ theo dõi và không mở thao tác sửa kế hoạch từ chat. Khi thuyết trình kỹ thuật, gọi cơ chế hiện tại là **định tuyến công cụ ở phía máy chủ** và **truy xuất từ khóa**. Kiểm tra cấu hình tên `Gemini:ApiKey`, `Gemini:Model` và `SmtpSettings` khi làm tích hợp. Chỉ bàn giao tên khóa, giữ giá trị bí mật trong môi trường riêng.

## 4. Công việc đã hoàn thành và thứ còn mở

| Phần | Tình trạng |
| --- | --- |
| Sửa trang chủ và thanh điều hướng | Đã triển khai. Khách thấy đăng ký/đăng nhập. Tài khoản thường thấy nút vào Dashboard, Admin thấy nút vào quản trị. Mục đang mở có nền và vạch xanh, kiểm tra cả menu thu gọn. |
| Nhóm 1 · Đồng bộ cân nặng | Đã triển khai, mốc **148/148 test**. Có đối chiếu cân, xác nhận thay đổi lớn, đồng bộ sau ghi/sửa/xóa và giữ ngày thực tế trên biểu đồ. [Báo cáo Nhóm 1](WEIGHT_SYNC_PHASE_2_REPORT.md) |
| Nhóm 2 · Giao diện và nhãn | Đã triển khai, mốc **156/156 test**. Dashboard gộp cảnh báo, phân biệt mốc TDEE; lịch sử cân phân trang **15 bản ghi/trang** và biểu đồ vẫn dùng toàn bộ lịch sử; các nhật ký liên quan có nhãn ngày `vi-VN`. Nhóm này đã kiểm tra HTML qua HTTP; lượt kiểm tra trực quan riêng chưa thực hiện được. [Báo cáo Nhóm 2](UI_PHASE_2_REPORT.md) |
| Nhóm 3 · Chọn tốc độ | Đã triển khai phương án **giữ giới hạn năng lượng hiện hành**, mốc **169/169 test**. Đã thao tác cả Tạo mục tiêu và Onboarding trên trình duyệt QA, gồm chọn nhanh, xác nhận và chặn thời hạn ngắn. Đã xem Tạo mục tiêu ở 375 px. Các bảng và ngưỡng nâng trần trong bản đề xuất cũ không phải hành vi hiện tại. [Báo cáo Nhóm 3](GOAL_PACE_PHASE_2_REPORT.md) |
| **8 câu bắt buộc** cho người từ 18 tuổi và bản xây lại | Có đặc tả riêng. Ứng dụng hiện tại vẫn dùng 4 câu. Các ý trong bản xây lại như trạng thái vòng đời mục tiêu, mục tiêu Duy trì riêng khi đạt đích và lịch sử ngân sách theo ngày là yêu cầu xây mới. [Prompt xây lại](PROMPT_XAY_LAI_NUTRITRACKAI.md) |
| Trần theo bậc BMI và dung sai 10% | Đã hoãn trong lần chốt Nhóm 3. Nếu được yêu cầu làm sau, cập nhật đặc tả và kiểm thử riêng. Một ví dụ cũ nêu TDEE `2.251,56`, thâm hụt `780`, mức nạp còn `1.471,56`, thấp hơn sàn `1.500` của chính cấu hình. |

Mốc **169/169** lấy từ lượt chạy sau Nhóm 3. Ba nhóm dùng host QA InMemory, giữ nguyên schema, LocalDB và dữ liệu `test2`. Kiểm tra dịch vụ thật, trạng thái migration của LocalDB và giao dịch dưới tải đồng thời SQL Server chưa có kết quả xác minh cho bản mới. Trong workspace tại lúc bàn giao, `git rev-parse` không nhận ra Git repository. Kiểm tra lại nếu làm việc trên một bản clone khác.

## 5. Chạy lại và bảo vệ dữ liệu

Mở PowerShell trong workspace. Bản QA dưới đây dùng dữ liệu giả lập và không chạy entry point sản xuất.

```powershell
Set-Location -LiteralPath 'D:\DACN\NutriTrackAI'
dotnet test '.\NutriTrackAI.Tests\NutriTrackAI.Tests.csproj' -c Group3 --no-restore -p:UseSharedCompilation=false
dotnet run --project '.\NutriTrackAI.Tests\NutriTrackAI.Tests.csproj' -c Group3 --no-build --no-restore -- --serve
```

Nếu chưa có assets NuGet, restore project test trước khi chạy lệnh test. Host QA dùng `http://127.0.0.1:5168`, có `/qa/signin` trong chế độ `--serve`. Dừng bằng `Ctrl+C`. Lần kiểm thử Nhóm 3 có cảnh báo `NU1900` vì không tải được dữ liệu kiểm tra lỗ hổng của gói, test vẫn chạy xong.

Ứng dụng thật dùng cấu hình `ConnectionStrings:DefaultConnection`. Profile `https` của Visual Studio mở `https://localhost:7212`, profile `http` dùng `http://localhost:5127`. `Program.cs` có thể seed khi khởi động. Ba migration có trong source là `InitialCreate`, `AddAdaptiveCaloriePlan` và `AddHealthScreening`. Development chỉ tự áp dụng nếu `AddAdaptiveCaloriePlan` là migration pending duy nhất. Các trường hợp khác cần kiểm tra database đích và lịch sử migration trước khi thao tác. Dùng `QA InMemory` khi chưa cần ghi database. Báo cáo demo 42 ngày là ảnh chụp kết quả ở một thời điểm, cần đọc lại dữ liệu nếu người dùng hỏi trạng thái hiện tại của `test2`. [Xử lý LocalDB](LOCALDB_STARTUP_FIX.md) · [Sàng lọc và migration](HEALTH_SCREENING.md) · [Demo 42 ngày](DEMO_TEST2_COMPLETE_42_DAYS.md)

## 6. Cách nhận việc trong cuộc trò chuyện mới

Khi tôi giao nhiệm vụ mới, đọc mã và báo cáo của phần liên quan trước. Giữ các quyết định ở mục 3 và 4 trừ khi nhiệm vụ mới thay đổi chúng. Nếu sửa code, dùng dữ liệu QA để xác minh luồng bình thường và ca biên. Báo rõ tệp đã sửa, bài kiểm thử đã chạy, và phần còn cần xác minh. Khi tôi hỏi về **bản xây lại**, dùng `docs/PROMPT_XAY_LAI_NUTRITRACKAI.md`. Khi tôi hỏi về **bản đang chạy**, đối chiếu mã trong workspace. Có thể đọc thêm [bản bàn giao kỹ thuật chi tiết](AI_HANDOFF.md) để xem bản đồ theo route và các mốc lịch sử.
