# BÁO CÁO TỔNG HỢP TRIỂN KHAI NHÓM 1, 2 VÀ 3

**Đồ án** · NutriTrackAI  
**Phạm vi** · Đợt 2 theo `CODEX_PROMPT_2.md` và các quyết định đã chốt khi triển khai  
**Kết quả kiểm thử cuối đợt** · 169/169 bài đạt trên dữ liệu giả lập

## 1. Mục tiêu của đợt triển khai

NutriTrackAI là website tiếng Việt hỗ trợ theo dõi cân nặng, ăn uống, nước uống và vận động, đồng thời lập mục tiêu calo và gợi ý thực đơn. Đợt 2 xử lý ba vấn đề quan sát được khi sử dụng ứng dụng. Nhóm 1 sửa sai lệch giữa cân Hồ sơ và Nhật ký cân. Nhóm 2 làm rõ các trạng thái và số liệu trên giao diện. Nhóm 3 bổ sung lựa chọn tốc độ khi đặt mục tiêu cân nặng, cùng bước xác nhận ở máy chủ.

Các thay đổi được triển khai trên ASP.NET Core MVC, Razor Views và EF Core hiện có. Kiểm thử tích hợp dùng host QA với EF InMemory. Cả ba nhóm sử dụng schema hiện hành và không ghi dữ liệu vào LocalDB hay tài khoản demo `test2`.

## 2. Nhóm 1 · Đồng bộ cân nặng giữa Hồ sơ và Nhật ký

**Vấn đề.** Trước đây, sửa cân ở Hồ sơ chỉ cập nhật `UserProfile.CurrentWeightKg`. Dashboard dùng trường này để tính ngân sách calo, trong khi biểu đồ đọc các bản ghi `WeightLog`. Hai phần có thể cùng hiển thị hai số khác nhau. Ví dụ tái hiện trên QA là Hồ sơ **85,0 kg** và Nhật ký gần nhất **70,0 kg**.

**Cách xử lý.** Tách nghiệp vụ ghi, tra cứu mốc gần nhất, xóa và đồng bộ vào `WeightTrackingService`. Dịch vụ được dùng ở Hồ sơ, Nhật ký cân và Onboarding. Mốc mới nhất được chọn theo ngày ghi rồi theo ID giảm dần. Ngày ghi dùng ngày nghiệp vụ Việt Nam.

| Tình huống | Hành vi sau khi triển khai |
| --- | --- |
| Đổi cân trong Hồ sơ | Tạo mốc hôm nay hoặc cập nhật mốc đã có cùng ngày. Nếu nhập đúng số cân ở mốc gần nhất, đồng bộ Hồ sơ với mốc đó. |
| Lưu Hồ sơ khi đang có dữ liệu lệch | Nếu người dùng sửa trường khác và giữ nguyên số cân đã hiển thị, cả hai giá trị được giữ để người dùng chủ động đối chiếu. |
| Ghi, sửa hoặc xóa trong Nhật ký | Sửa theo ID và quyền sở hữu. Chuyển bản ghi sang ngày đã có mốc khác sẽ báo lỗi. Khi mốc mới nhất thay đổi, ứng dụng cập nhật cân Hồ sơ và tính lại ngân sách trong cùng giao dịch. Nếu xóa mốc cuối cùng, giữ cân Hồ sơ làm mốc ban đầu. |
| Thay đổi cân lớn | Yêu cầu xác nhận trước khi ghi nếu mức chênh lớn hơn `max(2,0 kg, 3% × cân tham chiếu)` và mốc tham chiếu cách ngày ghi từ 0 đến 14 ngày. Áp dụng ở Hồ sơ và Nhật ký. |
| Xử lý số liệu cũ bị lệch | Dashboard, Hồ sơ và Nhật ký cân hiện cả hai giá trị và ngày của mốc gần nhất. Người dùng chọn ghi cân Hồ sơ cho hôm nay hoặc dùng cân Nhật ký. Máy chủ kiểm tra lại dữ liệu hiển thị và antiforgery trước khi ghi. |
| Hiển thị biểu đồ | Tiến độ dùng cân đã ghi. Khi không có bản ghi trong cửa sổ 30 ngày nhưng người dùng có lịch sử, biểu đồ Dashboard dùng mốc gần nhất với ngày ghi thực tế. Cho tới khi đối chiếu, ngân sách vẫn tính theo cân Hồ sơ. |

**Kiểm chứng.** Có **148/148 bài kiểm thử đạt** sau Nhóm 1. Các ca kiểm tra bao gồm ghi cùng ngày, xác nhận ở cả hai biểu mẫu, sửa/xóa mốc mới nhất, hai hướng đối chiếu, yêu cầu đã cũ, quyền sở hữu, antiforgery và biểu đồ với mốc cân cũ. [Đặc tả Nhóm 1](WEIGHT_SYNC_PHASE_2.md) · [Báo cáo Nhóm 1](WEIGHT_SYNC_PHASE_2_REPORT.md) · [Dịch vụ cân nặng](../Services/WeightTrackingService.cs)

## 3. Nhóm 2 · Làm rõ giao diện và các trạng thái theo dõi

**Vấn đề.** Dashboard lặp lý do tạm ngừng, số calo đã nạp có thể hiện `−0` và phần gợi ý món có thể báo thiếu món dù chức năng đang tạm ngừng. Dashboard, Nhật ký ăn và chatbot dùng các nhãn khác nhau cho cùng mốc năng lượng. Nhật ký cân tải toàn bộ hàng lịch sử và định dạng ô ngày phụ thuộc trình duyệt.

**Thay đổi đã triển khai**

1. Dashboard hiển thị lý do tạm ngừng một lần ở đầu trang, kèm liên kết tới Hồ sơ và Mục tiêu. Lý do lấy từ dịch vụ kế hoạch và dữ liệu sàng lọc hiện có. Ghi chú sức khỏe cá nhân được giữ ở Hồ sơ.
2. Dashboard, Nhật ký ăn và ngữ cảnh chatbot thống nhất **Ngân sách calo** khi có kế hoạch hiệu lực và **Mốc duy trì (TDEE)** khi kế hoạch tạm ngừng hoặc chưa có mục tiêu. Nhật ký ăn hiển thị ví dụ **Còn 2.786 kcal**. Dashboard hiển thị `0` khi chưa nạp calo.
3. Thẻ cân ghi rõ khi mục tiêu vẫn còn nhưng kế hoạch calo đang tạm ngừng. Phần gợi ý món giải thích trạng thái này và dẫn tới trang Mục tiêu. Hàng ghi nước nhanh được căn về cuối thẻ.
4. Việt hóa các nhãn chế độ ăn và dinh dưỡng như **Cân bằng**, **Chất đạm**, **Tinh bột**, **Chất béo**, **Chất xơ** và **Đường**. Chân trang dùng tiêu đề **Cảnh báo y tế quan trọng**. Trang Cân nặng bỏ nhãn “Chart.js Live”.
5. Phân trang lịch sử ở máy chủ với **15 bản ghi mỗi trang**. Biểu đồ và tổng biến động vẫn dùng đủ lịch sử. Sửa, xóa và đối chiếu giữ ngữ cảnh trang. Bộ dữ liệu QA 43 bản ghi cho ba trang lần lượt **15, 15 và 13** dòng.
6. Thêm nhãn ngày `vi-VN` ở Nhật ký ăn, Cân nặng và Nước & Vận động, ví dụ **Thứ Bảy, 14/11/2026**. Nhãn cập nhật khi chọn ngày hoặc khi biểu mẫu cân đổi ngày lúc sửa/hủy sửa. Cách hiển thị trong ô chọn ngày gốc vẫn do trình duyệt quyết định.

**Kiểm chứng.** Có **156/156 bài kiểm thử đạt** sau Nhóm 2, gồm 8 ca mới. Đã kiểm tra HTML qua HTTP, ngữ cảnh chatbot ở ba trạng thái kế hoạch, số dòng từng trang và nhãn ngày. Đã chạy kiểm tra cú pháp và hành vi cập nhật nhãn ngày trong Node. Lượt kiểm tra trực quan riêng của Nhóm 2 chưa thực hiện được vì công cụ trình duyệt không khởi tạo. [Báo cáo Nhóm 2](UI_PHASE_2_REPORT.md) · [Dịch vụ ngân sách calo](../Services/CalorieBudgetService.cs)

## 4. Nhóm 3 · Chọn tốc độ khi đặt mục tiêu cân nặng

**Vấn đề.** Người dùng chỉ nhập số tuần nên khó biết tốc độ tương ứng. Cách tính cũ có thể giới hạn mức điều chỉnh ban đầu ở mức thông thường dù thời hạn người dùng chọn vẫn nằm trong giới hạn hồ sơ. Kết quả là ngày dự kiến có thể dài hơn thời hạn đã chọn.

**Thay đổi đã triển khai**

1. Trang **Tạo mục tiêu** và **Onboarding** có ba nút **Nhẹ nhàng**, **Theo mức tham chiếu** và **Nhanh nhất trong giới hạn**. Mỗi nút hiện số tuần nguyên và tốc độ ước tính tương ứng. Người dùng vẫn có thể nhập tuần thủ công. Đổi cân đích hoặc hồ sơ sẽ tính lại bản xem trước và giữ số tuần đã nhập đến khi người dùng chọn một đề xuất.
2. Máy chủ tính và kiểm tra mức cần theo thời hạn trước khi lưu. Với kế hoạch hợp lệ, mức điều chỉnh khởi tạo bằng mức cần theo số tuần. Khi bật **giai đoạn làm quen** trên trang Tạo mục tiêu, mức áp dụng tối đa **250 kcal/ngày**. Bản xem trước hiện riêng tốc độ mong muốn và tốc độ ước tính theo mức áp dụng.
3. Kế hoạch giảm **trên 0,5 kg/tuần** và còn trong giới hạn hồ sơ yêu cầu xác nhận trên cả hai biểu mẫu. Mức vượt giới hạn bị chặn tại máy chủ. Giao diện dùng nhãn chữ để giải thích vùng tốc độ. Duy trì và chế độ chỉ theo dõi ẩn các lựa chọn nhanh.
4. Giữ trần, sàn calo, BMI và sàng lọc hiện hành. Phiên bản này dùng **Nhanh nhất trong giới hạn** theo từng hồ sơ. Ngưỡng `0,75 kg/tuần` từ bản đề xuất cũ vượt tốc độ có thể lập theo trần thâm hụt `750 kcal/ngày` của ứng dụng. Đề xuất nâng trần theo bậc BMI và dung sai 10% được hoãn.

### Ví dụ nghiệm thu trên dữ liệu giả lập

| Kịch bản | Kết quả |
| --- | --- |
| Mục tiêu 90 → 85 kg, TDEE 3.200 kcal/ngày, thời hạn 7 tuần | Cần khoảng 786 kcal/ngày. Máy chủ chặn vì vượt trần 750 kcal/ngày. |
| Cùng hồ sơ, chọn Nhanh nhất trong giới hạn | Đề xuất 8 tuần, tương ứng 0,625 kg/tuần và mức điều chỉnh 687,5 kcal/ngày. Biểu mẫu yêu cầu xác nhận. |
| Cùng hồ sơ và thời hạn 8 tuần, bật làm quen | Giữ thời hạn mong muốn, áp dụng 250 kcal/ngày và hiển thị tốc độ theo mức làm quen khoảng 0,23 kg/tuần. |
| Onboarding trên hồ sơ QA kiểm tra bằng trình duyệt | Chọn nhanh nhất điền 12 tuần. Kết quả cuối hiển thị khoảng 0,56 kg/tuần và ngân sách 1.966 kcal/ngày. |

**Kiểm chứng.** Có **169/169 bài kiểm thử đạt** sau Nhóm 3, gồm 13 ca mới. Đã kiểm tra trên trình duyệt cả Tạo mục tiêu và Onboarding, bao gồm chọn nhanh, hiện ô xác nhận, chặn thời hạn quá ngắn và hiển thị Tạo mục tiêu ở chiều rộng 375 px. [Báo cáo Nhóm 3](GOAL_PACE_PHASE_2_REPORT.md) · [Dịch vụ lập mục tiêu](../Services/GoalPlanningService.cs)

## 5. Tổng hợp kiểm thử và phạm vi xác minh

| Mốc | Bài kiểm thử tích lũy đạt | Trọng tâm |
| --- | --- | --- |
| Sau Nhóm 1 | 148/148 | Đồng bộ dữ liệu cân, xác nhận và đối chiếu. |
| Sau Nhóm 2 | 156/156 | Nhãn, trạng thái, phân trang và định dạng ngày. |
| Sau Nhóm 3 | 169/169 | Tính tốc độ, giới hạn năng lượng và xác nhận ở hai luồng lưu. |

Ba hàng là các mốc của cùng bộ kiểm thử qua từng đợt. Lệnh tái hiện mốc cuối từ gốc dự án sau khi đã restore gói NuGet:

```powershell
dotnet test NutriTrackAI.Tests/NutriTrackAI.Tests.csproj -c Group3 --no-restore -p:UseSharedCompilation=false
```

Kiểm thử tích hợp dùng HTTP trên EF InMemory. Bản mới chưa được xác minh về giao dịch đồng thời trên SQL Server, tình trạng migration của LocalDB, hoặc chất lượng phản hồi từ Gemini thật. Lượt build có cảnh báo `NU1900` vì môi trường không tải được dữ liệu kiểm tra lỗ hổng gói. Các số calo và tốc độ trong bảng là kết quả kiểm thử phần mềm trên hồ sơ giả lập.
