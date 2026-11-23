# Báo cáo Nhóm 2 · Làm rõ giao diện NutriTrackAI

## Kết quả triển khai

| Hạng mục | Thay đổi |
| --- | --- |
| Dashboard và lý do tạm ngừng | Hiển thị lý do một lần ở đầu trang, kèm liên kết tới sàng lọc trong Hồ sơ và trang Mục tiêu. Dùng lý do từ dịch vụ đang áp dụng. Điều kiện dựa trên `HealthNotes` trong prompt không còn tồn tại, nên ghi chú riêng tư không được đưa ra Dashboard. |
| Nhãn calo | Dùng **Ngân sách calo** khi có kế hoạch hiệu lực. Khi tạm ngừng hoặc chưa có mục tiêu, dùng **Mốc duy trì (TDEE)**. Dashboard, Nhật ký ăn và ngữ cảnh chatbot dùng cùng khái niệm. Nhật ký ăn hiển thị dạng **Còn 2.786 kcal**. Dashboard hiển thị **0** khi chưa nạp calo. |
| Gợi ý món, thẻ cân và thẻ nước | Phần gợi ý món giải thích trạng thái tạm ngừng và dẫn tới trang Mục tiêu. Thẻ cân giữ tên mục tiêu và ghi rõ kế hoạch calo tạm ngừng. Hàng ghi nước nhanh căn về cuối thẻ. |
| Tiếng Việt | Dashboard, Nhật ký ăn và Hồ sơ dùng nhãn tiếng Việt cho chế độ ăn và dinh dưỡng ở các phần liên quan. Chân trang hiển thị **Cảnh báo y tế quan trọng**. |
| Lịch sử cân nặng | Máy chủ tải tối đa 15 hàng lịch sử mỗi trang qua tham số `page`. Biểu đồ và tổng biến động dùng toàn bộ các mốc. Các thao tác sửa, xóa và đối chiếu giữ ngữ cảnh trang lịch sử. Đã bỏ nhãn “Chart.js Live”. |
| Ngày được chọn | Nhật ký ăn, Cân nặng và Nước & Vận động có nhãn thứ và ngày tiếng Việt, ví dụ **Thứ Bảy, 14/11/2026**. Ô chọn ngày gốc hiển thị theo ngôn ngữ và thiết lập của trình duyệt. |

Các phần liên quan gồm [Dashboard](../Views/Dashboard/Index.cshtml), [Nhật ký ăn](../Views/MealLog/Index.cshtml), [lịch sử cân nặng](../Controllers/WeightLogController.cs), [dịch vụ ngân sách](../Services/CalorieBudgetService.cs) và [ngữ cảnh chatbot](../Services/UserContextBuilder.cs).

## Xác minh

```powershell
dotnet test NutriTrackAI.Tests/NutriTrackAI.Tests.csproj -c Group2 --no-restore -p:UseSharedCompilation=false
node --check wwwroot/js/business-date.js
```

**156/156 bài kiểm thử đạt**, gồm 8 ca mới qua host QA InMemory. Các ca mới kiểm tra cảnh báo tạm ngừng và nhãn Dashboard, ba trạng thái kế hoạch trong Nhật ký ăn và ngữ cảnh chatbot, phân trang lịch sử `15 / 15 / 13` với 43 bản ghi, và nhãn ngày tiếng Việt ở ba màn hình. Đã kiểm tra cú pháp JavaScript và thử cập nhật nhãn khi chọn, đổi hoặc xóa ngày trong Node.

Kiểm tra trực quan bằng trình duyệt chưa thực hiện được vì công cụ không khởi tạo. NuGet báo `NU1900` vì không tải được dữ liệu kiểm tra lỗ hổng của gói. LocalDB, schema và dữ liệu `test2` không được cập nhật. Chính sách tốc độ giảm cân ở Nhóm 3 nằm ngoài đợt này.
