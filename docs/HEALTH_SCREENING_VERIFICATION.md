# Nghiệm thu Health Screening NutriTrackAI

Ngày 10/11/2026, giờ Việt Nam. Workspace `D:\DACN\NutriTrackAI`.

## 1. Audit và môi trường

- ASP.NET Core MVC nhắm `net8.0`, SDK .NET 10.0.401, EF Core trong dự án 8.0.33. Build và test dùng cấu hình `Release`. Cấu hình website `http` và `https` đều đặt môi trường `Development`.
- Cấu hình kết nối Development trỏ đến `(localdb)\MSSQLLocalDB`, database `NutriTrackAIDb`, xác thực Windows. LocalDB báo phiên bản 17.0.4025.3. Xác minh database đích bằng `DB_NAME()` trên kết nối SQL thật. Không đọc hay đưa thông tin xác thực vào báo cáo.
- Đối chiếu cấu hình DI, model EF, migration, controller và view với tài liệu. `GoalPlanningService`, `CalorieBudgetService` và `AdaptiveCalorieService` được đăng ký theo scope. Tạo mục tiêu, xác nhận điều chỉnh và đánh giá xu hướng đều kiểm tra trạng thái máy chủ. Các mốc BMI người lớn 18,5 / 23 / 25, giới hạn theo TDEE, cân nặng, sàn calo và trần 750 vẫn thuộc cùng chính sách.
- LocalDB trong sandbox không đọc được cấu hình registry. Đã thực hiện truy vấn metadata, sao lưu và migration bằng phiên PowerShell có quyền truy cập LocalDB của tài khoản Windows. Các thao tác SQL chỉ nhắm database Development đã xác minh.

## 2. Sao lưu, migration và schema SQL Server thật

Trước triển khai, lịch sử có `20261026094736_InitialCreate` và `20261107105859_AddAdaptiveCaloriePlan`. EF xác nhận chỉ có `20261110053547_AddHealthScreening` đang chờ. Kiểm tra migration tiến xác nhận chỉ thêm cột nullable, không tạo bảng hay sửa dữ liệu nghiệp vụ. [Migration](../Migrations/20261110053547_AddHealthScreening.cs)

Sao lưu `COPY_ONLY, CHECKSUM` trước migration. `RESTORE VERIFYONLY WITH CHECKSUM` xác nhận bản sao lưu hợp lệ. Tệp cục bộ nằm ở `.db-backups/NutriTrackAIDb-before-AddHealthScreening-20261110-124856-668.bak` và có dung lượng 7.954.944 byte. Tệp chứa dữ liệu database thật, giữ riêng trên máy. Xác minh này dùng `VERIFYONLY`; chưa chạy thử khôi phục vào database riêng.

Đã áp dụng migration bằng EF Core Release, chỉ định môi trường Development. Truy vấn sau đó xác nhận migration xuất hiện trong `__EFMigrationsHistory` và `dbo.user_profiles.HealthScreeningJson` là `nvarchar(max) NULL` (`is_nullable=1`, `max_length=-1`).

| Đối chiếu | Trước | Sau |
| --- | ---: | ---: |
| Số bảng, gồm lịch sử migration | 24 | 24 |
| Tài khoản | 5 | 5 |
| Hồ sơ | 4 | 4 |
| Mục tiêu | 11 | 11 |
| Nhật ký cân nặng | 46 | 46 |
| Nhật ký bữa ăn | 180 | 180 |

Cả bốn hồ sơ cũ giữ `HealthScreeningJson = NULL` sau migration. Không dùng reset database hay tạo tài khoản QA trên LocalDB thật.

## 3. Build và kiểm thử

- Restore thành công từ các gói sẵn có. Project test báo `NU1900` vì môi trường không tải được dữ liệu kiểm tra lỗ hổng từ NuGet. Lượt build ứng dụng Release kết thúc với **0 lỗi, 0 cảnh báo**.
- `dotnet test` Release đạt **128 Passed, 0 Failed, 0 Skipped** sau khi bổ sung các kiểm tra nghiệm thu. Unit test kiểm tra phân loại và công thức. HTTP integration test dùng host ASP.NET Core Development và EF InMemory với dữ liệu giả lập, có xác thực và antiforgery thật trong ứng dụng.
- Edge headless chạy trên localhost với host QA Development InMemory. Tài khoản, câu trả lời và nhật ký dùng trong trình duyệt là dữ liệu giả lập. Kiểm tra cả 5 màn hình ở **1440, 768 và 390 px**. Đo độ rộng nội dung trong trình duyệt xác nhận cả 15 trường hợp không tràn ngang.

## 4. Kịch bản nghiệm thu

| Kịch bản | Bằng chứng | Kết quả |
| --- | --- | --- |
| Chưa sàng lọc | Edge và HTTP | Đăng ký, bỏ trống câu trả lời, hoàn tất Onboarding và vào Dashboard theo dõi. HTTP kiểm tra ghi cân, bữa ăn và nước, không sinh mục tiêu và từ chối tạo mục tiêu bằng dữ liệu sàng lọc giả từ form. |
| Bốn câu Không | Edge, unit và HTTP | Profile lưu được, trường JSON có ngày UTC, trạng thái là tự khai chưa ghi nhận yếu tố. Tạo và xác nhận mục tiêu 85 → 80 kg với đề xuất 10 tuần trong ca QA. |
| Có hoặc Chưa chắc chắn | Edge, unit và HTTP | Có và Chưa chắc chắn đều hiển thị cần đánh giá chuyên môn. API check-in từ chối đề xuất tăng cho hồ sơ hạn chế. |
| Cập nhật trở lại Không | Edge và HTTP | Kế hoạch cũ vẫn tạm ngừng, mục tiêu cũ và mức điều chỉnh đã xác nhận vẫn lưu. |
| Hồ sơ cũ NULL | SQL thật, unit, HTTP và Edge với hồ sơ QA tương đương | SQL có bốn hồ sơ NULL. Mã đọc NULL an toàn, Dashboard/Profile vẫn hoạt động và không suy ra đủ điều kiện. |
| Công thức và đánh giá | Unit và HTTP | BMR 1.667,5 và TDEE 2.584,625 cho nam 24 tuổi, 170 cm, 72 kg, Moderate. Kiểm tra trần theo TDEE, cân nặng, trần 750, sàn calo, BMI đích, BMI người dưới 18 tuổi, check-in 7 ngày và đánh giá 14 ngày. |
| Quyền riêng tư và xác thực | Audit mã, unit và HTTP | Chặn truy cập ẩn danh, POST thiếu antiforgery và thử sửa hồ sơ bằng ID người khác. Admin không được trả chi tiết sàng lọc. JSON lỗi hoặc phiên bản không hỗ trợ không mở khóa kế hoạch. Luồng chatbot hạn chế trả tóm tắt nhật ký tại máy chủ, không gửi câu trả lời sàng lọc đến Gemini. |
| Tích hợp ngân sách và thực đơn | HTTP và InMemory | Bộ kiểm thử đối chiếu Goal, Dashboard, MealLog, ngữ cảnh chatbot và ngân sách chung. Meal Planner giữ bộ lọc dị ứng, món không thích và loại chế độ ăn. |

## 5. Ảnh QA

Ảnh nằm trong `.qa/health-screening/`. Mỗi trang có đủ ba độ rộng. Các ảnh mobile dưới đây là mẫu.

- [Onboarding](../.qa/health-screening/onboarding-390.png)
- [Profile](../.qa/health-screening/profile-390.png)
- [Goal/Create](../.qa/health-screening/goal-create-390.png)
- [Goal/Index](../.qa/health-screening/goal-index-390.png)
- [Dashboard](../.qa/health-screening/dashboard-390.png)

## 6. Tệp thay đổi và phạm vi còn lại

- [WorkflowTests.cs](../NutriTrackAI.Tests/WorkflowTests.cs) bổ sung kiểm tra thời điểm lưu sàng lọc và thao tác ghi cả ba loại nhật ký khi chưa có kế hoạch.
- [check-onboarding-preview.cjs](../.qa/check-onboarding-preview.cjs) cập nhật bộ kiểm tra JavaScript đã lệch chữ ký hàm. Bổ sung kiểm tra payload sàng lọc và cho phép tiếp tục chỉ theo dõi.
- [health-screening-browser.cjs](../.qa/health-screening-browser.cjs) chạy Edge headless với host QA, kiểm tra luồng và lưu ảnh.
- Báo cáo này và 15 ảnh QA. Không cần sửa mã nghiệp vụ trong đợt nghiệm thu.

Kiểm tra SQL thật giới hạn ở sao lưu, migration, schema và số lượng bản ghi. Luồng thao tác được kiểm thử với dữ liệu QA InMemory. Chưa gửi yêu cầu Gemini thật, chưa kiểm thử nhà cung cấp SMTP và chưa phục hồi thử bản sao lưu.

## 7. Hướng dẫn demo

1. Trong PowerShell mở workspace, chạy `dotnet run --project NutriTrackAI.Tests/NutriTrackAI.Tests.csproj -c Release -- --serve`, sau đó mở `http://127.0.0.1:5168`. Dữ liệu QA sẽ mất khi dừng host.
2. Đăng ký tài khoản giả lập và bỏ trống bốn câu hỏi. Hoàn tất onboarding, quan sát cảnh báo trên Dashboard rồi mở nhật ký.
3. Vào Hồ sơ, chọn bốn câu Không và lưu. Tạo mục tiêu giảm cân hợp lệ, xem dự báo và xác nhận.
4. Chọn Có hoặc Chưa chắc chắn ở một câu, lưu rồi mở Mục tiêu. Sau đó trả lời lại Không và quan sát kế hoạch cũ vẫn tạm ngừng.
5. Xem riêng check-in cảm nhận 7 ngày và đánh giá xu hướng 14 ngày trên mục tiêu QA đủ mốc thời gian. Có thể dùng host QA đã seed qua `http://127.0.0.1:5168/qa/signin?path=/Goal`.
6. Để chạy lại toàn bộ kiểm tra giao diện tự động, dừng host demo rồi chạy `node .qa/health-screening-browser.cjs` sau khi build cả project test ở Release.
