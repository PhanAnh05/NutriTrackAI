# Demo hoàn tất 6 tuần — test2@gmail.com

Đã áp dụng theo lựa chọn rõ ràng của người dùng: dịch toàn bộ lộ trình 42 ngày về quá khứ để xem hoàn thành ngay trên /Goal. Chỉ cập nhật test2 (UserId 1004, GoalId 10), không đổi schema, không tạo database mới, không thay đổi công thức production.

## Kết quả

- Lộ trình giả lập: 26/09–07/11/2026 (42 ngày), giữ 72 → 70 kg.
- Giữ 14 ngày cân đã demo, dịch ngày về đúng kỳ đầu; bổ sung 28 ngày còn lại. Tổng cộng 43 bản ghi: 1 mốc bắt đầu + 42 ngày theo dõi.
- Cân hiện tại 70 kg, đã giảm 2 kg, tiến trình 100%.
- Giữ đúng mức điều chỉnh đã xác nhận: 366.6666666667 kcal/ngày, không tự tăng theo tuần.
- 42 ngày nhật ký ăn, 168 bữa, chọn món từ database qua bộ lọc của MealPlannerService. Dị ứng cua và món không thích cháo được kiểm tra lại trên dữ liệu lưu.
- Ngân sách ngày cuối 2187 kcal/ngày; TDEE mới 2553.625. Service đề xuất duy trì với điều chỉnh 0 và ngân sách làm tròn 2554, CHƯA tự áp dụng.

## Cách tính lượng nạp đúng dự án

Dùng NutritionCalculatorService và CalorieBudgetService cho từng cân nặng của ngày đó; không lấy ngân sách ngày cuối áp cho toàn bộ 42 ngày.

```text
BMR = 10W + 6.25 × 170 − 5 × 24 + 5
TDEE = BMR × 1.55
Ngân sách ngày = RoundAwayFromZero(TDEE − 366.6666666667)
Phân bổ: sáng 25%, trưa 35%, tối 30%, phụ là phần còn lại khoảng 10%.
```

Danh sách món được chọn qua MealPlannerService (giữ lọc dị ứng, món không thích, chế độ ăn). Khẩu phần được căn theo ngân sách từng bữa và làm tròn 2 chữ số như độ chính xác Servings trong DB. Mỗi snapshot dinh dưỡng được tính theo đúng thứ tự phép tính MealLogController:

```text
ratio = Servings / ServingSize
Calories = Math.Round(Food.Calories × ratio, 2)
Protein/Carb/Fat/Fiber/Sugar/Sodium tính tương tự
```

Không gán calo tùy ý để ép tổng. Truy vấn độc lập AsNoTracking đã đối chiếu calo, protein, carb, fat với món và khẩu phần thực lưu. Sai số lớn nhất giữa tổng nạp và ngân sách chỉ 0.03 kcal/ngày do làm tròn; trong giới hạn ±10% của dự án và nhỏ hơn 0.5% được đặt làm điều kiện commit của demo.

## Các mốc chính (kcal/ngày)

| Ngày | Cân nặng | Ngân sách | Nạp thực lưu |
| --- | ---: | ---: | ---: |
| 1 | 72.0 kg | 2218 | 2218.00 |
| 7 | 71.8 kg | 2215 | 2214.99 |
| 14 | 71.4 kg | 2209 | 2209.01 |
| 21 | 71.1 kg | 2204 | 2204.01 |
| 28 | 70.7 kg | 2198 | 2198.02 |
| 35 | 70.1 kg | 2189 | 2189.01 |
| 42 | 70.0 kg | 2187 | 2187.00 |

## Bốn bữa ngày cuối

| Bữa | Ngân sách | Nạp thực lưu | Món |
| --- | ---: | ---: | --- |
| Sáng | 547 | 547.00 | Bún chả Hà Nội |
| Trưa | 765 | 765.00 | Cơm gạo lứt, Trứng gà luộc, Bông cải xanh luộc |
| Tối | 656 | 656.00 | Bánh mì nguyên cám, Thịt lợn nạc luộc, Dưa leo tươi |
| Phụ | 219 | 219.00 | Chuối tiêu |

## Đánh giá định kỳ

- Ngày 14: đủ 14 ngày cân và 14 ngày ăn; xu hướng khoảng 0.3429 kg/tuần; không đề xuất đổi thâm hụt. Ghi nhận giữ nguyên kế hoạch trong dữ liệu giả lập.
- Ngày 28: đủ dữ liệu, xu hướng khoảng 0.3714 kg/tuần; tiếp tục giữ mức điều chỉnh ban đầu.
- Ngày 42: trung bình nhóm 7 ngày đầu kỳ cuối 70.4 kg, nhóm 7 ngày cuối 70 kg; xu hướng 0.4 kg/tuần. GoalReached=true, ProposedAdjustment=0, MayIncrease=false. Để trạng thái chờ người dùng xác nhận duy trì trên /Goal.

Đây là dữ liệu mô phỏng để kiểm tra phần mềm; lượng calo không bảo đảm người thật sẽ giảm theo đúng đường cân nặng giả lập này.

## Bảng đầy đủ 42 ngày

| Ngày | Ngày lịch | Cân nặng (kg) | Ngân sách kcal | Nạp thực lưu kcal |
| --- | --- | ---: | ---: | ---: |
| 1 | 2026-09-27 | 72.0 | 2218 | 2218.00 |
| 2 | 2026-09-28 | 72.1 | 2220 | 2220.01 |
| 3 | 2026-09-29 | 72.0 | 2218 | 2218.00 |
| 4 | 2026-09-30 | 71.9 | 2216 | 2215.98 |
| 5 | 2026-10-01 | 71.8 | 2215 | 2214.98 |
| 6 | 2026-10-02 | 71.9 | 2216 | 2215.99 |
| 7 | 2026-10-03 | 71.8 | 2215 | 2214.99 |
| 8 | 2026-10-04 | 71.7 | 2213 | 2212.98 |
| 9 | 2026-10-05 | 71.8 | 2215 | 2214.99 |
| 10 | 2026-10-06 | 71.6 | 2212 | 2212.02 |
| 11 | 2026-10-07 | 71.6 | 2212 | 2212.01 |
| 12 | 2026-10-08 | 71.5 | 2210 | 2209.98 |
| 13 | 2026-10-09 | 71.5 | 2210 | 2210.01 |
| 14 | 2026-10-10 | 71.4 | 2209 | 2209.01 |
| 15 | 2026-10-11 | 71.4 | 2209 | 2209.02 |
| 16 | 2026-10-12 | 71.3 | 2207 | 2207.00 |
| 17 | 2026-10-13 | 71.4 | 2209 | 2209.00 |
| 18 | 2026-10-14 | 71.2 | 2206 | 2206.01 |
| 19 | 2026-10-15 | 71.2 | 2206 | 2206.02 |
| 20 | 2026-10-16 | 71.1 | 2204 | 2203.98 |
| 21 | 2026-10-17 | 71.1 | 2204 | 2204.01 |
| 22 | 2026-10-18 | 71.0 | 2202 | 2201.99 |
| 23 | 2026-10-19 | 71.0 | 2202 | 2202.01 |
| 24 | 2026-10-20 | 70.9 | 2201 | 2201.00 |
| 25 | 2026-10-21 | 70.9 | 2201 | 2201.01 |
| 26 | 2026-10-22 | 70.8 | 2199 | 2199.00 |
| 27 | 2026-10-23 | 70.8 | 2199 | 2199.01 |
| 28 | 2026-10-24 | 70.7 | 2198 | 2198.02 |
| 29 | 2026-10-25 | 70.7 | 2198 | 2198.02 |
| 30 | 2026-10-26 | 70.6 | 2196 | 2196.00 |
| 31 | 2026-10-27 | 70.5 | 2195 | 2195.00 |
| 32 | 2026-10-28 | 70.4 | 2193 | 2192.99 |
| 33 | 2026-10-29 | 70.3 | 2192 | 2191.99 |
| 34 | 2026-10-30 | 70.2 | 2190 | 2190.01 |
| 35 | 2026-10-31 | 70.1 | 2189 | 2189.01 |
| 36 | 2026-11-01 | 70.0 | 2187 | 2187.00 |
| 37 | 2026-11-02 | 70.0 | 2187 | 2187.00 |
| 38 | 2026-11-03 | 70.0 | 2187 | 2187.03 |
| 39 | 2026-11-04 | 70.0 | 2187 | 2187.00 |
| 40 | 2026-11-05 | 70.0 | 2187 | 2186.98 |
| 41 | 2026-11-06 | 70.0 | 2187 | 2187.01 |
| 42 | 2026-11-07 | 70.0 | 2187 | 2187.00 |

## Kiểm tra và sao lưu

Đã chạy thử rollback trước khi áp dụng. Trong quá trình kiểm tra có một transaction rollback do kiểm tra thứ tự phép tính làm tròn; đã sửa trình demo để dùng chính xác thứ tự của controller và chỉ commit khi toàn bộ kiểm tra đạt. Sau commit, kiểm tra lại bằng GoalController/AdaptiveCalorieService và các truy vấn AsNoTracking: đúng 100%, 70 kg, 2187 kcal, 42 ngày ăn, 168 bữa, đúng khẩu phần và bộ lọc.

- Sao lưu trước demo 42 ngày: `.qa/test2-complete42/before.json`.
- Bảng dữ liệu đã áp dụng: `.qa/test2-complete42/applied.json`.
- Kết quả kiểm tra độc lập: `.qa/test2-complete42/verified-final.json`.
- Trình demo và trình kiểm tra: `.qa/test2-complete42/Complete.cs.txt`; build CompleteRun thành công 0 lỗi/0 cảnh báo. Không thay đổi code nghiệp vụ production trong tác vụ này.
- Bản sao giai đoạn trước 14 ngày vẫn ở `.qa/test2-demo14/`; script restore 14 ngày cũ không còn phù hợp sau demo 42 ngày và sẽ chặn theo điều kiện ngày/số bản ghi. Cần dùng backup mới nếu muốn hoàn tác; không tự chạy script cũ.

## Thao tác xem

1. Đăng nhập test2@gmail.com trên ứng dụng hiện tại, tải lại /Goal: 70 kg, 100%, dự kiến đã đạt đích.
2. Mở /WeightLog xem chuỗi 6 tuần; /MealLog chọn ngày 07/11 hoặc các ngày trong bảng để đối chiếu 4 bữa/ngày.
3. Dashboard trước xác nhận duy trì vẫn dùng ngân sách giảm cân ngày cuối 2187. Lượng vận động giả lập bằng 0; calo còn lại chỉ dao động vài phần trăm kcal do làm tròn món ăn.
4. Tại /Goal, xem đề xuất chuyển duy trì; đánh dấu xác nhận rồi áp dụng nếu muốn hoàn tất bước duy trì. Sau đó ngân sách mới khoảng 2554 kcal/ngày. Các nhật ký 42 ngày giữ nguyên số liệu lịch sử.

## Ứng dụng đang phục vụ demo

Đã mở ứng dụng local trên https://localhost:7212 (và http://localhost:5127), HTTP /Account/Login trả 200. Dùng named pipe hiện hành qua biến môi trường riêng tiến trình vì LocalDB manager vẫn có lúc báo Stopped dù SQL engine đang chạy; không ghi cứng pipe vào file cấu hình. Nếu chạy lại bằng F5, cần dừng tiến trình demo đang chiếm cổng trước. Tác vụ này hoàn tất dữ liệu demo; không coi việc mở phiên demo là đã sửa tận gốc lỗi LocalDB manager trước đó.
