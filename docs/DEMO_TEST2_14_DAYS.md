# Demo ngày thứ 14 — test2@gmail.com

Thực hiện theo xác nhận cho phép cập nhật riêng test2 trên SQL Server trong cuộc trò chuyện. UserId 1004, GoalId 10; không sửa tài khoản khác, không đổi schema.

## Mục tiêu giữ nguyên về nội dung

72 → 70 kg, 6 tuần, thâm hụt chính xác 366.6666666667 kcal/ngày. Đã dịch ngày bắt đầu từ 07/11 về 24/10/2026, ngày mong muốn từ 19/12 về 05/12/2026 để mô phỏng đã qua 14/42 ngày, còn 28 ngày. Mốc đánh giá trước là 24/10; hiện đến kỳ 07/11. Không tự bấm giữ/áp dụng đánh giá và không thay đổi mức thâm hụt.

Nhật ký khởi tạo ID 4 được chuyển sang 24/10, cân 72 kg. Thêm 14 bản ghi giả lập có Note đánh dấu DEMO14_TEST2_20261107. Tổng lịch sử gồm 1 mốc khởi tạo + 14 ngày theo dõi; cửa sổ gần nhất vẫn đúng 14 ngày.

| Ngày theo dõi | Cân nặng (kg) |
| --- | ---: |
| 25/10/2026 | 72.0 |
| 26/10/2026 | 72.1 |
| 27/10/2026 | 72.0 |
| 28/10/2026 | 71.9 |
| 29/10/2026 | 71.8 |
| 30/10/2026 | 71.9 |
| 31/10/2026 | 71.8 |
| 01/11/2026 | 71.7 |
| 02/11/2026 | 71.8 |
| 03/11/2026 | 71.6 |
| 04/11/2026 | 71.6 |
| 05/11/2026 | 71.5 |
| 06/11/2026 | 71.5 |
| 07/11/2026 | 71.4 |

## Kết quả được xác minh bằng code ứng dụng

- CurrentWeightKg = 71.4; khớp bản ghi cuối.
- Đã giảm 0.6 kg; tiến trình 0.6 / 2 * 100 = 30%.
- TDEE mới 2575.325 kcal/ngày; giữ thâm hụt 366.6666666667 nên ngân sách làm tròn 2209.
- IsDue = true; SufficientData = true; WeightDays = 14; MealDays = 0.
- Trung bình 7 ngày đầu: 71.9285714286 kg; 7 ngày cuối: 71.5857142857 kg. Ngày trung bình hai nhóm cách nhau 7 ngày.
- Xu hướng giảm: (71.9285714286 − 71.5857142857) / 7 * 7 = 0.3428571429 kg/tuần.
- Tốc độ từ đầu cửa sổ 25/10 đến cuối cửa sổ 07/11: (72 − 71.4) / 13 * 7 = 0.3230769231 kg/tuần.
- Tốc độ từ mốc khởi tạo 24/10 đến 07/11: 0.6 / 14 * 7 = 0.3 kg/tuần. Khác chỉ số cửa sổ do khác mốc đầu.
- ProposedAdjustment = null; MayIncrease = false; GoalReached = false.
- Thông báo: giữ mức điều chỉnh hiện tại, cập nhật TDEE, chưa đủ căn cứ đề xuất tăng mức điều chỉnh.
- Không tạo nhật ký ăn giả. Có thể đánh giá xu hướng cân nhưng không kết luận tuân thủ ăn uống khi thiếu nhật ký ăn.

Trình kiểm tra gọi GoalController.Index và AdaptiveCalorieService trên đúng database test2; kiểm tra tiến trình 30%, 14 ngày, đến kỳ đánh giá, không đề xuất đổi calo, ngân sách 2209, và ChangeTracker không có thay đổi. Build trình kiểm tra DemoAudit đạt 0 lỗi/0 cảnh báo. Không thay đổi giờ hệ thống hoặc logic production.

## Xem demo

Đăng nhập test2@gmail.com ở ứng dụng hiện tại, tải lại /WeightLog rồi /Goal. Kiểm tra 71.4 kg, 30%, 14 ngày và xu hướng khoảng 0.34 kg/tuần. Mở Dashboard để đối chiếu ngân sách 2209. Nếu bấm Giữ kế hoạch hiện tại, ngày đánh giá tiếp theo sẽ chuyển đến 21/11/2026. Demo chưa đạt đích 70 kg nên không được đề xuất duy trì.

## Sao lưu và hoàn tác

Bản sao trước khi sửa: `.qa/test2-demo14/before.json`. Kết quả kiểm tra: `.qa/test2-demo14/verified.json`. Script áp dụng: `.qa/test2-demo14/apply.sql`. Script hoàn tác CHƯA CHẠY: `.qa/test2-demo14/restore.sql`, có kiểm tra tránh ghi đè sau khi người dùng thay đổi thêm dữ liệu. Nếu đã tiếp tục thao tác sau demo thì cần kiểm tra lại trước khi hoàn tác.
