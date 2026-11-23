# Báo cáo triển khai Nhóm 1 · Đồng bộ cân nặng

**Dự án** NutriTrackAI  
**Phạm vi** Nhóm 1 của `CODEX_PROMPT_2.md`  
**Trạng thái** Đã triển khai và kiểm thử trên dữ liệu giả lập

## 1. Lỗi ban đầu

Khi người dùng đổi cân ở Hồ sơ, ứng dụng chỉ cập nhật `UserProfile.CurrentWeightKg`. Dashboard dùng số này để tính ngân sách calo, trong khi biểu đồ đọc các bản ghi `WeightLog`. Hai phần có thể hiển thị số khác nhau, chẳng hạn Hồ sơ là **85,0 kg**, còn Nhật ký gần nhất là **70,0 kg**. Số liệu này là ví dụ tái hiện lỗi trong môi trường QA.

## 2. Kết quả triển khai

| Luồng | Hành vi sau khi sửa |
| --- | --- |
| Đổi cân trong Hồ sơ | Tạo bản ghi theo ngày Việt Nam hoặc cập nhật bản ghi đã có hôm nay. Khi nhập đúng cân của bản ghi gần nhất, đồng bộ Hồ sơ với bản ghi đó. Lưu trường khác khi hai số cân đang lệch giữ nguyên sai lệch để người dùng đối chiếu. |
| Ghi, sửa và xóa trong Nhật ký | Giữ thao tác sửa theo ID và kiểm tra chủ sở hữu. Nếu chuyển mốc sang ngày đã có bản ghi khác, biểu mẫu báo lỗi. Khi sửa hoặc xóa mốc làm thay đổi cân hiện tại, cập nhật Hồ sơ từ mốc gần nhất và tính lại ngân sách trong cùng giao dịch. Nếu xóa hết nhật ký, giữ cân hồ sơ làm mốc ban đầu. |
| Thay đổi lớn | Hồ sơ và Nhật ký yêu cầu xác nhận trước khi lưu nếu chênh lệch lớn hơn `max(2,0 kg, 3% × cân tham chiếu)` và mốc tham chiếu cách ngày ghi từ 0 đến 14 ngày. Nếu biểu mẫu phải hiển thị lại, giữ cân, ngày và ghi chú. Ảnh mới cần chọn lại. |
| Đối chiếu dữ liệu cũ | Dashboard, Hồ sơ và Nhật ký cân hiện hai số cân cùng ngày cân gần nhất. Người dùng chọn **Ghi nhận cân Hồ sơ cho hôm nay** hoặc **Dùng cân Nhật ký**. Máy chủ kiểm tra quyền sở hữu, antiforgery và giá trị hiển thị trước khi lưu. |
| Tiến độ và biểu đồ | Dashboard và Nhật ký dùng mốc cân đã ghi để hiển thị tiến độ. Nếu Dashboard không có mốc trong cửa sổ 30 ngày nhưng đã có lịch sử, biểu đồ hiển thị mốc gần nhất với ngày thực tế. Khi hai nguồn còn lệch, ngân sách calo tiếp tục dùng cân Hồ sơ cho đến khi cập nhật hoặc đối chiếu. |
| Onboarding | Dùng chung dịch vụ ghi mốc cân ban đầu theo ngày Việt Nam. |

Quy tắc xử lý các trường hợp biên nằm trong [đặc tả Nhóm 1](WEIGHT_SYNC_PHASE_2.md).

## 3. Mã nguồn liên quan

- Dịch vụ tập trung thao tác ghi, lấy mốc gần nhất, xóa và đồng bộ cân. [WeightTrackingService](../Services/WeightTrackingService.cs)
- Các điểm vào sử dụng dịch vụ gồm Hồ sơ, Nhật ký, Dashboard và Onboarding. [ProfileController](../Controllers/ProfileController.cs) · [WeightLogController](../Controllers/WeightLogController.cs) · [DashboardController](../Controllers/DashboardController.cs) · [OnboardingController](../Controllers/OnboardingController.cs)
- Cảnh báo dùng chung và thông báo xác nhận được hiển thị trên các trang liên quan. [Cảnh báo đối chiếu](../Views/Shared/_WeightMismatchBanner.cshtml) · [Hồ sơ](../Views/Profile/Index.cshtml) · [Nhật ký cân](../Views/WeightLog/Index.cshtml)
- Kiểm thử hồi quy chạy qua HTTP với tài khoản và dữ liệu giả lập. [WorkflowTests](../NutriTrackAI.Tests/WorkflowTests.cs)

## 4. Kết quả kiểm thử

**148/148 bài kiểm thử đạt, 0 thất bại, 0 bỏ qua.** Bộ kiểm thử xác nhận ghi từ Hồ sơ, cập nhật cùng ngày, giữ sai lệch khi lưu trường khác, xác nhận thay đổi lớn ở cả hai biểu mẫu, kiểm tra mốc trùng ngày, hai lựa chọn đối chiếu, từ chối yêu cầu có dữ liệu đã thay đổi hoặc thiếu antiforgery, cách ly dữ liệu giữa tài khoản và giữ ngày thật của bản ghi cân trên biểu đồ. Ca kiểm thử ngân sách đã thêm xác nhận cho bước giảm từ `71,7` xuống `69 kg`, đồng thời kiểm tra mức thâm hụt và lịch sử bữa ăn.

```powershell
dotnet test NutriTrackAI.Tests/NutriTrackAI.Tests.csproj -c WeightSyncPhase2 --no-restore -p:UseSharedCompilation=false
```

Build hoàn tất với cảnh báo `NU1900` vì môi trường không tải được dữ liệu kiểm tra lỗ hổng từ NuGet. HTML được kiểm tra qua HTTP trên QA InMemory. Phiên kiểm tra trực quan bằng trình duyệt không khởi tạo được. Kiểm thử giao dịch dưới tải đồng thời trên SQL Server vẫn cần thực hiện trong môi trường test riêng.

## 5. Phạm vi dữ liệu và bàn giao

Đợt triển khai dùng schema hiện tại và dữ liệu QA InMemory. LocalDB và dữ liệu `test2` không được cập nhật. Các hạng mục dọn giao diện ở Nhóm 2 và thay đổi chính sách tốc độ giảm cân ở Nhóm 3 nằm ngoài báo cáo này.
