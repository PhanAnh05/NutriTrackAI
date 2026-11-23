# Kiểm tra dữ liệu QA /Goal — 07/11/2026

## Kết luận

Có lỗi seed QA, không phải tình huống cân nặng dao động về 72 kg. Seed cũ giữ UserProfile.CurrentWeightKg = 72 nhưng tạo 28 WeightLog bằng 72 - 0.01 * day (day = 0..27). Lần cuối thực tế là 71.73 kg. GoalController.Index lấy cân hiện tại và tiến trình từ profile, còn AdaptiveCalorieService lấy xu hướng từ WeightLogs. Profile không đồng bộ dẫn đến 72 kg / 0% trên màn hình dù nhật ký giảm. InMemory cũng không áp dụng làm tròn cột decimal(5,1) như SQL Server, nên bước 0.01 kg không phù hợp fixture của ứng dụng dùng độ chính xác 0.1 kg.

Đã thay dữ liệu trong project test bằng QaWeightData (28 ngày, 0.1 kg, có dao động nhỏ), gán CurrentWeightKg từ phần tử cuối và tính lại ngân sách qua CalorieBudgetService. Không sửa dữ liệu SQL Server, schema, migration, Identity hoặc cách tính tiến trình production. Thay đổi chung duy nhất là các trường thống kê đọc để giải thích trên UI và dùng decimal khi tính ngày trung bình, giữ nguyên định nghĩa xu hướng và quy tắc đề xuất.

## Toàn bộ 14 ngày đang đánh giá (kg)

| Ngày | Seed cũ | Seed đã sửa |
| --- | ---: | ---: |
| 25/10/2026 | 71.86 | 71.9 |
| 26/10/2026 | 71.85 | 72.0 |
| 27/10/2026 | 71.84 | 71.9 |
| 28/10/2026 | 71.83 | 71.8 |
| 29/10/2026 | 71.82 | 71.9 |
| 30/10/2026 | 71.81 | 71.8 |
| 31/10/2026 | 71.80 | 71.9 |
| 01/11/2026 | 71.79 | 71.8 |
| 02/11/2026 | 71.78 | 71.9 |
| 03/11/2026 | 71.77 | 71.8 |
| 04/11/2026 | 71.76 | 71.7 |
| 05/11/2026 | 71.75 | 71.8 |
| 06/11/2026 | 71.74 | 71.7 |
| 07/11/2026 | 71.73 | 71.7 |

Đây là 14 ngày cuối của chuỗi 28 ngày. Chuỗi 28 ngày được giữ vì đề xuất tăng mức điều chỉnh cần đối chiếu hai chu kỳ, không chỉ một chu kỳ.

## Công thức chính xác

Các lần cân cùng ngày được gộp thành trọng lượng trung bình ngày. Trong kỳ Today-13..Today:

- Nhóm đầu: Today-13..Today-7 (7 ngày lịch).
- Nhóm cuối: Today-6..Today (7 ngày lịch).
- Mỗi ngày có dữ liệu được tính một lần; dùng trung bình các ngày có ghi, không tự nội suy ngày thiếu.
- Khoảng cách là ngày trung bình của nhóm cuối trừ ngày trung bình của nhóm đầu. Khi đủ 14 ngày liên tiếp, khoảng cách bằng 7 ngày.

Với giảm cân, dấu dương biểu thị giảm:

```text
TrendRate = (MeanWeightFirstGroup - MeanWeightLastGroup)
            / (MeanDateLastGroup - MeanDateFirstGroup).TotalDays * 7
EndpointRate = (FirstRecordedWeight - LastRecordedWeight)
               / (LastRecordedDate - FirstRecordedDate).TotalDays * 7
```

Không phải hồi quy tuyến tính, không phải lấy tổng giảm chia 14 ngày. 14 lần cân hằng ngày bao phủ 13 ngày từ mốc đầu đến mốc cuối. Nếu thiếu dữ liệu, mẫu ngày trung bình có thể khác 7 ngày; code dùng khoảng cách thực tế đó.

Seed cũ: nhóm đầu trung bình 71.83 kg, nhóm cuối 71.76 kg. Trend = (71.83 - 71.76) / 7 * 7 = 0.07 kg/tuần. Đầu–cuối = (71.86 - 71.73) / 13 * 7 = 0.07 kg/tuần. Hai số bằng nhau vì seed cũ giảm đều tuyến tính 0.01 kg/ngày, không do dao động.

Seed mới: nhóm đầu trung bình 71.8857142857 kg, nhóm cuối 71.7714285714 kg. Trend = 0.1142857143 kg/tuần. Đầu–cuối = (71.9 - 71.7) / 13 * 7 = 0.1076923077 kg/tuần. Hai số khác nhau vì các phép thống kê dùng các mốc khác nhau và dữ liệu có dao động. UI định dạng hai chữ số thập phân nên đều hiện 0.11; đã bổ sung nhãn, trung bình nhóm, thời gian kỳ và giải thích cách tính, không coi chúng là một chỉ số.

## Kết quả mong đợi

| Chỉ số | Kết quả |
| --- | ---: |
| Cân nặng bắt đầu mục tiêu | 72.0 kg |
| Cân nặng hiện tại (lần cân cuối) | 71.7 kg |
| Đã giảm từ bắt đầu mục tiêu | 0.3 kg |
| Mục tiêu | 65.0 kg |
| Tiến trình chính xác | 0.3 / 7 * 100 = 4.285714% |
| Tiến trình UI (cắt phần lẻ theo code hiện tại) | 4% |
| Đã giảm trong kỳ 14 lần cân gần đây | 0.2 kg |
| Tốc độ xu hướng | 0.1142857143 kg/tuần |
| Tốc độ đầu–cuối | 0.1076923077 kg/tuần |
| Số ngày cân / số ngày ghi ăn gần đây | 14 / 14 |
| TDEE theo 71.7 kg | 2579.975 kcal/ngày |
| Thâm hụt giữ nguyên | 500 kcal/ngày |
| Ngân sách (làm tròn) | 2080 kcal/ngày |

Mức khoảng 0.11 mô tả quá trình giả lập giảm chậm, không phải dự báo hoặc kết luận y tế. Tiến trình dùng mốc bắt đầu toàn bộ mục tiêu, còn tốc độ dùng kỳ 14 ngày; cùng chuỗi dữ liệu nhưng khác phạm vi thời gian.

## Đầy đủ 28 ngày của seed mới

| Ngày | Cân nặng (kg) |
| --- | ---: |
| 11/10/2026 | 72.0 |
| 12/10/2026 | 72.1 |
| 13/10/2026 | 72.0 |
| 14/10/2026 | 72.0 |
| 15/10/2026 | 71.9 |
| 16/10/2026 | 72.0 |
| 17/10/2026 | 72.0 |
| 18/10/2026 | 71.9 |
| 19/10/2026 | 72.0 |
| 20/10/2026 | 71.9 |
| 21/10/2026 | 71.9 |
| 22/10/2026 | 72.0 |
| 23/10/2026 | 71.9 |
| 24/10/2026 | 71.9 |
| 25/10/2026 | 71.9 |
| 26/10/2026 | 72.0 |
| 27/10/2026 | 71.9 |
| 28/10/2026 | 71.8 |
| 29/10/2026 | 71.9 |
| 30/10/2026 | 71.8 |
| 31/10/2026 | 71.9 |
| 01/11/2026 | 71.8 |
| 02/11/2026 | 71.9 |
| 03/11/2026 | 71.8 |
| 04/11/2026 | 71.7 |
| 05/11/2026 | 71.8 |
| 06/11/2026 | 71.7 |
| 07/11/2026 | 71.7 |

## Kiểm tra thực tế và phạm vi

- 70/70 test Passed, 0 Failed. Test mới kiểm tra từng giá trị của cả 28 ngày, độ chính xác 0.1 kg, profile bằng lần cân cuối, hai trung bình, ngày trung bình, hai tốc độ, tiến trình controller và HTTP /Goal.
- Các test điều chỉnh calo cập nhật expected theo cân nặng QA mới; không thay expected cho công thức ở hồ sơ độc lập 72 kg.
- Test chạy trên configuration QaAudit để tránh file lock của host QA đang mở. Sau đó xác minh tiến trình cổng 5168 là đúng NutriTrackAI.Tests.exe, khởi động lại riêng host này và build Release thành công với 0 warnings / 0 errors.
- HTTP thực tế tại /qa/signin?path=/Goal xác nhận 71.7 kg, 4%, hai nhãn tốc độ, kỳ 25/10–07/11, 2080 kcal/ngày.
- Không chạy dotnet ef, SQL script hoặc truy vấn SQL Server. Host và test dùng Microsoft.EntityFrameworkCore.InMemory. Không dừng tiến trình production.
- Đây là dữ liệu QA giả lập; ngày sẽ dịch theo GoalPolicy.Today khi khởi động lại vào ngày khác.

## File thay đổi

- NutriTrackAI.Tests/QaWeightData.cs: nguồn dữ liệu QA duy nhất.
- NutriTrackAI.Tests/QaApplication.cs: seed nhật ký, đồng bộ profile và ngân sách.
- NutriTrackAI.Tests/WorkflowTests.cs: kiểm tra đồng bộ và expected theo dữ liệu mới.
- Services/AdaptiveCalorieService.cs: cung cấp các thành phần thống kê đọc cho UI, ngày trung bình dùng decimal.
- Views/Goal/Index.cshtml: giải thích hai tốc độ và phạm vi của tiến trình.
- docs/QA_WEIGHT_AUDIT.md: báo cáo này.
