# Sàng lọc sức khỏe trước khi lập kế hoạch calo

## Audit trước khi sửa

- Luồng Onboarding trước đây bắt buộc tạo được mục tiêu mới để hoàn tất hồ sơ. Dùng cùng một cổng chặn cho cả onboarding và mục tiêu sẽ khiến người chưa khai báo mất quyền vào nhật ký.
- Ngân sách được tính lại ở nhiều màn hình. Chỉ ẩn nút tăng thâm hụt trên giao diện sẽ bỏ sót các POST tạo mục tiêu, điều chỉnh theo cảm nhận và đánh giá xu hướng.
- Kế hoạch cũ lưu mức đã xác nhận trong `CaloriePlanJson`. Nếu chỉ sửa kết quả sàng lọc trong hồ sơ, hệ thống có thể tự khôi phục mức cũ khi điều kiện thay đổi.
- Ghi chú sức khỏe tự do chưa phân biệt thiếu câu trả lời với tình trạng cần xem xét. Luồng chatbot và màn hình quản trị cần tránh xuất các câu trả lời riêng tư.
- Check-in cảm nhận sau bảy ngày và đánh giá xu hướng sau 14 ngày có mục đích và mốc thời gian riêng.

## Hành vi sau khi triển khai

Migration [AddHealthScreening](../Migrations/20261110053547_AddHealthScreening.cs) thêm một cột nullable `nvarchar(max)` tên `HealthScreeningJson` vào `user_profiles`. Số bảng giữ nguyên. Hồ sơ cũ có giá trị NULL, kể cả khi có ghi chú tự do. JSON lưu phiên bản, thời điểm UTC và bốn câu tự khai dạng lựa chọn. Máy chủ suy ra trạng thái, không nhận trạng thái hoặc JSON đã tính từ client.

| Câu trả lời | Trạng thái và hành vi |
| --- | --- |
| Thiếu câu trả lời, không có câu nào Có hoặc Chưa chắc chắn | Chưa hoàn tất sàng lọc. Có thể hoàn tất hồ sơ và dùng nhật ký mà không tạo mục tiêu tự động. |
| Bốn câu đều Không | Đã khai báo, chưa ghi nhận yếu tố cần đánh giá thêm. Hệ thống vẫn áp dụng quy tắc tuổi, BMI, TDEE, trần theo cân nặng, trần 750 kcal và sàn calo hiện tại. |
| Ít nhất một câu Có hoặc Chưa chắc chắn | Cần đánh giá chuyên môn. Tạm ngừng kế hoạch calo tự động, giữ quyền ghi và xem nhật ký. |

Các câu hỏi bao quát thai kỳ hoặc cho con bú, bệnh lý hoặc điều trị có liên quan, tiền sử rối loạn ăn uống và sụt cân ngoài ý muốn hoặc triệu chứng chưa đánh giá. Không yêu cầu nhập bệnh án. Tự khai không thay thế đánh giá của chuyên gia. [NIDDK Body Weight Planner](https://www.niddk.nih.gov/health-information/weight-management/body-weight-planner)

Khi hồ sơ chuyển sang trạng thái hạn chế, ngân sách hiệu lực trở về mốc TDEE tham khảo, không còn thâm hụt. Kế hoạch, lịch sử và cờ cảnh báo cũ được giữ. Kể cả khi hoàn tất sàng lọc sau đó, app không tự kích hoạt lại mức cũ. Người dùng cần xem và xác nhận một mục tiêu mới. Với hồ sơ đang bị hạn chế, Dashboard ngừng gợi ý theo ngân sách, bộ sinh thực đơn tự động tạm ngừng, còn chatbot trả số liệu nhật ký tại máy chủ. Câu trả lời sàng lọc không được gửi sang Gemini hay đưa vào trang quản trị.

Mốc BMI **18,5 / 23 / 25** là cách NutriTrackAI trình bày phân loại BMI người trưởng thành theo Bảng 4.1 của Quyết định 2892/QĐ-BYT. App gộp các mức từ 25 vào nhãn “Béo phì” và không dùng phân loại người lớn cho người dưới 18 tuổi. [Quyết định 2892/QĐ-BYT, bản văn bản ký](https://benhvientantao.com/wp-content/uploads/2022/11/quyet-dinh-2892-qd-byt-bo-y-te.pdf)

## Kiểm thử trên website

Để thử với SQL Server, sao lưu database thử nghiệm, kiểm tra kết nối đang trỏ đúng database và áp dụng migration theo quy trình EF của dự án. Có thể chạy lệnh sau tại thư mục dự án để áp dụng tới phiên bản mới.

```powershell
dotnet ef database update 20261110053547_AddHealthScreening --project NutriTrackAI.csproj -- --environment Development
dotnet run --project NutriTrackAI.csproj --launch-profile http
```

Để thử với dữ liệu giả lập, chạy `dotnet run --project NutriTrackAI.Tests/NutriTrackAI.Tests.csproj -c HealthScreening -- --serve` và mở `http://127.0.0.1:5168`. Cách này không cần migration và không ghi LocalDB. [Hướng dẫn host QA](AI_HANDOFF.md)

1. Đăng ký tài khoản thử. Để trống khai báo hoặc chọn một câu Có. Hoàn tất Onboarding, vào Dashboard và mở Nhật ký ăn. Xác nhận app hiển thị chế độ theo dõi và không tạo mục tiêu calo.
2. Vào Hồ sơ. Chọn bốn câu Không rồi lưu. Tạo mục tiêu giảm hợp lệ, chẳng hạn người trưởng thành 85 → 80 kg với hồ sơ đáp ứng giới hạn. Kiểm tra preview và ngân sách sau khi xác nhận.
3. Đổi một câu sang Chưa chắc chắn. Kiểm tra Dashboard và Mục tiêu hiển thị tạm ngừng, các luồng tăng thâm hụt không áp dụng được. Đổi lại Không và xác nhận mục tiêu cũ vẫn tạm ngừng. Mở nhật ký để xác nhận dữ liệu cũ còn nguyên.
4. Với mục tiêu đang có hiệu lực, phân biệt khu vực phản hồi cảm nhận sau bảy ngày với phần đánh giá xu hướng cân và nhật ký sau 14 ngày. Kiểm tra riêng trên các hồ sơ thử phù hợp với hai mốc.
5. Kiểm tra vai trò Admin chỉ thấy dữ liệu quản trị đã có. Mở trang Hồ sơ khi chưa đăng nhập để xác nhận được chuyển về đăng nhập.

## Tệp thay đổi

- Dữ liệu: `Models/Entities/HealthScreening.cs`, `Models/Entities/UserProfile.cs`, `Models/Entities/CaloriePlanState.cs`, hai tệp migration `20261110053547_AddHealthScreening*` và `Migrations/AppDbContextModelSnapshot.cs`.
- Chính sách và dịch vụ: `GoalPlanningService.cs`, `CalorieBudgetService.cs`, `AdaptiveCalorieService.cs`, `IUserContextBuilder.cs`, `UserContextBuilder.cs`, `GeminiChatService.cs`, `MealPlannerService.cs` trong `Services/`.
- MVC: `OnboardingController.cs`, `ProfileController.cs`, `GoalController.cs`, `DashboardController.cs` trong `Controllers/`, hai ViewModel Onboarding và Profile, cùng `Areas/Admin/Controllers/SystemSelfTestController.cs`.
- View: `Views/Shared/EditorTemplates/HealthScreeningAnswers.cshtml`, `Views/Onboarding/Index.cshtml`, `Views/Profile/Index.cshtml`, `Views/Goal/Index.cshtml`, `Views/Goal/_EnergyAnalysis.cshtml`, `Views/Dashboard/Index.cshtml`.
- Kiểm thử: `NutriTrackAI.Tests/PlanningTests.cs`, `NutriTrackAI.Tests/WorkflowTests.cs`, `NutriTrackAI.Tests/QaApplication.cs`.
- Tài liệu: `README.md` và báo cáo này.

## Kết quả xác minh

- `dotnet build` ở cấu hình `HealthScreening` thành công với 0 lỗi và 0 cảnh báo.
- `dotnet test` đạt 128/128. Kiểm thử tích hợp chạy trên EF InMemory, bao phủ hoàn tất onboarding chỉ theo dõi, trạng thái sàng lọc, sửa hồ sơ, không khôi phục kế hoạch cũ, dữ liệu giả từ client, quyền truy cập và chống CSRF.
- SQL tiến từ migration trước chỉ gồm thêm cột nullable và ghi lịch sử migration trong một transaction. Chưa áp dụng lên LocalDB hoặc kiểm tra thao tác giao diện bằng trình duyệt.
