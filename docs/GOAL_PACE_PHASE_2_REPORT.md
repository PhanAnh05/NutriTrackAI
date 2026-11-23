# Nhóm 3 · Chọn tốc độ khi đặt mục tiêu cân nặng

## Phạm vi đã chốt

Triển khai lựa chọn tốc độ trên **Tạo mục tiêu** và **Onboarding**, với trần, sàn calo, điều kiện BMI và sàng lọc hiện hành. Các đề xuất tăng trần theo bậc BMI và dung sai 10% trong `CODEX_PROMPT_2.md` được hoãn. Chúng không nằm trong phép kiểm tra kế hoạch đang chạy hay các luồng đánh giá 7 và 14 ngày của đợt này.

## Nghiệp vụ và giao diện

- Máy chủ trả ba lựa chọn nhanh **Nhẹ nhàng**, **Theo mức tham chiếu** và **Nhanh nhất trong giới hạn**. Lựa chọn giảm cân dùng mức tham chiếu lần lượt là 0,25 kg/tuần, 0,5 kg/tuần và giới hạn năng lượng của hồ sơ. Nút hiển thị số tuần nguyên cùng tốc độ ước tính theo chính số tuần đó. Mục tiêu Duy trì hoặc hồ sơ bị hạn chế sàng lọc sẽ không hiện các nút tốc độ.
- Mỗi lựa chọn chuyển mốc mong muốn sang số tuần nguyên bằng cách làm tròn lên, với tối thiểu 2 tuần. Khi người dùng sửa đích, loại mục tiêu hoặc hồ sơ, bản xem trước tính lại mà không tự sửa ô số tuần. Nút nhanh hoặc nút áp dụng thời gian đề xuất mới điền số tuần.
- Nếu số tuần đã chọn đáp ứng giới hạn, mức điều chỉnh ban đầu bằng mức cần theo thời hạn. Trang Tạo mục tiêu vẫn có lựa chọn làm quen, tối đa 250 kcal/ngày, và hiển thị riêng tốc độ theo thời hạn với tốc độ ước tính theo mức áp dụng. Máy chủ xác nhận lại toàn bộ dữ liệu trước khi lưu. Dữ liệu số trên màn hình được làm tròn để hiển thị. Phép kiểm tra giới hạn dùng giá trị chưa làm tròn.
- Giảm cân dưới 0,25 kg/tuần được ghi **Rất chậm**. Từ 0,25 đến dưới 0,5 là **Nhẹ nhàng**. Mức 0,5 là **Theo mức tham chiếu**. Trên 0,5 và còn trong trần là **Cao hơn mức tham chiếu**, cần xác nhận trên cả hai biểu mẫu. Đây là ngưỡng xác nhận của ứng dụng. Mốc trên 0,75 kg/tuần trong đặc tả cũ không thể đạt dưới trần 750 kcal/ngày hiện tại. Tăng cân dưới 0,1 kg/tuần được ghi **Rất chậm** và không yêu cầu xác nhận tốc độ cao.
- Giao diện dùng nhãn vùng bằng chữ. Mức tham chiếu 0,5 kg/tuần là một mốc và khoảng hợp lệ thay đổi theo hồ sơ, nên không dùng thanh chia thành bốn đoạn có chiều dài cố định.
- Tuyến `/Goal/Preview` giữ phản hồi HTML partial và tuyến `/Onboarding/CalculatePreview` giữ JSON. Hai biểu mẫu lấy vùng tốc độ, tuần và điều chỉnh từ cùng dịch vụ. Không cần migration hay sửa dữ liệu cũ.

## Ví dụ nghiệm thu

| Hồ sơ giả lập | Lựa chọn | Kết quả |
| --- | --- | --- |
| 90 → 85 kg, TDEE 3.200 kcal, giới hạn 750 kcal/ngày | 7 tuần | Cần khoảng 786 kcal/ngày. Bị chặn. |
| Cùng hồ sơ | Nhanh nhất trong giới hạn | 8 tuần, tốc độ ước tính 0,625 kg/tuần, điều chỉnh 687,5 kcal/ngày. Cần xác nhận. |
| Cùng hồ sơ | Chọn giai đoạn làm quen với thời hạn 8 tuần | Giữ thời hạn mong muốn. Mức áp dụng 250 kcal/ngày và tốc độ ước tính khoảng 0,23 kg/tuần. |
| 85 → 80 kg, nam, TDEE 1.800 kcal | Nhanh nhất trong giới hạn | 19 tuần, để mức nạp khoảng 1.511 kcal/ngày. Sàn của hệ thống là 1.500 kcal/ngày. |

## Kiểm thử

```powershell
dotnet test NutriTrackAI.Tests/NutriTrackAI.Tests.csproj -c Group3 --no-restore -p:UseSharedCompilation=false
node --check wwwroot/js/pace-picker.js
node --check wwwroot/js/goal-planning.js
```

**169/169 bài kiểm thử đạt**, gồm 13 ca mới. Các ca bao phủ tốc độ theo số tuần nguyên, ranh giới vùng, sàn calo, làm quen, Duy trì, hồ sơ sàng lọc hạn chế và hai thao tác lưu mục tiêu. Kiểm tra cú pháp JavaScript và script Onboarding đều đạt. Build có cảnh báo `NU1900` do môi trường không tải được dữ liệu kiểm tra lỗ hổng từ NuGet.

Đã kiểm tra trình duyệt trên QA InMemory. Trang Tạo mục tiêu hiển thị các nút ở độ rộng máy tính và 375 px. Chọn tốc độ nhanh nhất trên hồ sơ QA điền 12 tuần, yêu cầu xác nhận, và cho tiếp tục sau khi tích ô. Khi gõ 9 tuần, ô giữ nguyên 9 và kế hoạch bị chặn. Trong Onboarding, cùng hồ sơ cho kết quả cuối khoảng **0,56 kg/tuần và 1.966 kcal/ngày**. LocalDB và dữ liệu `test2` chưa được cập nhật.
