# GEMINI.md — Quy tắc & Ngữ cảnh Dự án (Project Context & Rules cho Antigravity)

## 1. Tổng quan Dự án
- **Tên đề tài**: Website hỗ trợ quản lý cân nặng và tư vấn dinh dưỡng cá nhân hóa tích hợp AI (`NutriTrackAI`).
- **Mục tiêu**: Hỗ trợ người dùng theo dõi cân nặng, BMI, BMR, TDEE, mục tiêu cân nặng, nhật ký bữa ăn, lượng nước, vận động, gợi ý thực đơn theo thuật toán và tư vấn dinh dưỡng cá nhân hóa qua Chatbot AI (Gemini API + RAG + Function Calling + Bộ lọc an toàn y tế).
- **Cảnh báo y tế bắt buộc (Medical Disclaimer)**:
  > "Thông tin trên website và nội dung do trợ lý AI tạo ra chỉ nhằm hỗ trợ theo dõi dinh dưỡng, cân nặng và lối sống, mang tính tham khảo. Hệ thống không phải thiết bị y tế, không chẩn đoán, không kê đơn và không thay thế ý kiến của bác sĩ, chuyên gia dinh dưỡng hoặc cơ sở y tế. Nếu bạn có bệnh lý, đang dùng thuốc, mang thai, hoặc có triệu chứng bất thường, hãy tham khảo chuyên môn trước khi thay đổi chế độ ăn."
  *(Hiển thị cố định ở Footer mọi trang chính và dưới mọi câu trả lời của Chatbot AI).*

## 2. Công nghệ Sử dụng (Tech Stack)
- **Framework**: ASP.NET Core 8.0 MVC (`net8.0`).
- **Xác thực & Phân quyền**: ASP.NET Core Identity (sử dụng khóa chính `int`, băm mật khẩu chuẩn Identity/PBKDF2, Cookie Authentication có `RememberMe` và Lockout).
- **ORM & Cơ sở dữ liệu**: Entity Framework Core 8.0 (Code-First) + SQL Server.
- **Giao diện (Frontend)**: Razor Views (`.cshtml`), Bootstrap 5.3 (Responsive Mobile & Desktop), Bootstrap Icons, Chart.js (vẽ biểu đồ cân nặng, calo, thống kê Admin), Fetch API / AJAX cho tương tác mượt mà (nhật ký nước, thêm món nhanh, khung chat AI).
- **Tích hợp AI**: Google Gemini API (`gemini-2.5-flash` cho hội thoại và Function Calling; `text-embedding-004` cho RAG trên bảng `Articles`).

## 3. Cấu trúc Thư mục Chuẩn
- `Controllers/`: Chứa các MVC Controller và API Controller (`HomeController`, `AccountController`, `OnboardingController`, `DashboardController`, `ProfileController`, `GoalController`, `WeightLogController`, `MealLogController`, `FoodController`, `MealPlanController`, `WaterExerciseController`, `ChatController`, và `Areas/Admin/Controllers/*`).
- `Models/Entities/`: Chứa 17 lớp thực thể CSDL chuẩn hóa.
- `Models/Enums/`: Chứa các Enum (`Gender`, `ActivityLevel`, `DietType`, `GoalType`, `MealType`, `ExerciseIntensity`, `ChatRole`, `ArticleType`).
- `Models/ViewModels/`: Chứa các ViewModel/DTO có gắn DataAnnotations phục vụ Validation trên Form và hiển thị View.
- `Data/`: Chứa `AppDbContext.cs` và `DbSeeder.cs` (khởi tạo tài khoản Admin, 6 nhóm thực phẩm, 100+ món ăn Việt Nam, danh mục bài tập và bài viết FAQ/Disclaimer).
- `Services/`:
  - `INutritionCalculatorService` / `NutritionCalculatorService`: Tính BMI, BMR, TDEE, Calo mục tiêu, Macro và kiểm tra ngưỡng an toàn.
  - `IMealPlannerService` / `MealPlannerService`: Thuật toán gợi ý thực đơn theo bữa, lọc dị ứng/món không thích, đổi món tương đương.
  - `IChatSafetyFilter` / `ChatSafetyFilter`: Kiểm tra từ khóa & ngữ cảnh rủi ro y tế trước khi gọi LLM.
  - `IUserContextBuilder` / `UserContextBuilder`: Tổng hợp hồ sơ, mục tiêu và nhật ký ăn/uống/tập ngày hôm nay thành chuỗi ngữ cảnh cho System Prompt.
  - `IFoodToolService` / `FoodToolService`: Cung cấp các hàm truy vấn dữ liệu thật (`get_today_nutrition`, `get_remaining_calories`, `search_foods`, `suggest_dinner`, `get_user_profile_summary`) cho Gemini Function Calling.
  - `IRagService` / `RagService`: Tìm kiếm ngữ nghĩa hoặc từ khóa trên bảng `Articles` (FAQ, bài viết dinh dưỡng) để đưa vào Prompt.
  - `IGeminiChatService` / `GeminiChatService`: Điều phối luồng chat, gọi Gemini API và lưu lịch sử.
- `Filters/`: Chứa `RequireOnboardingAttribute` (Action Filter bắt buộc User hoàn thành Onboarding trước khi vào Dashboard, Nhật ký, Thực đơn, Chatbot).

## 4. Quy tắc Nghiệp vụ & Công thức Toán học Bắt buộc

### 4.1. Chỉ số BMI
- Công thức dùng giá trị chưa làm tròn khi phân loại: `BMI = weightKg / (heightCm / 100)^2`
- Mốc sàng lọc người trưởng thành được ứng dụng chọn theo phân loại trong Bảng 4.1 của hướng dẫn Bộ Y tế. [Quyết định 2892/QĐ-BYT](https://emohbackup.moh.gov.vn/publish/attach/getfile/409620)
  - `< 18.5`: Thiếu cân
  - `18.5 ≤ BMI < 23`: Bình thường
  - `23 ≤ BMI < 25`: Thừa cân
  - `BMI ≥ 25`: Béo phì

### 4.2. Chỉ số BMR (Công thức Mifflin-St Jeor)
- Nam (`Gender.Male`): `BMR = 10 * W + 6.25 * H - 5 * A + 5`
- Nữ (`Gender.Female`): `BMR = 10 * W + 6.25 * H - 5 * A - 161`
- Khác (`Gender.Other`): `BMR = 10 * W + 6.25 * H - 5 * A - 78` (trung bình cộng Nam và Nữ, ghi chú rõ trên giao diện).
  *(Trong đó: `W` là cân nặng kg, `H` là chiều cao cm, `A` là tuổi tính theo năm).*

### 4.3. Chỉ số TDEE
- Công thức giữ đầy đủ độ chính xác cho bước lập kế hoạch: `TDEE = BMR * ActivityFactor`
- Hệ số vận động (`ActivityFactor`):
  - Ít vận động (`Sedentary`): `1.2`
  - Nhẹ (`Light`): `1.375`
  - Trung bình (`Moderate`): `1.55`
  - Nặng (`Active`): `1.725`
  - Rất nặng (`VeryActive`): `1.9`

### 4.4. Calo mục tiêu và giới hạn ứng dụng
- Duy trì (`Maintain`): `TargetCalories = TDEE`
- Giảm cân (`Lose`) trừ mức điều chỉnh đã xác nhận khỏi TDEE. Trần theo hồ sơ là `min(750, 25% × TDEE, 11 × cân nặng kg)`. Sàn 1.200 kcal với nữ và 1.500 với nam/khác giới hạn thêm mức thâm hụt.
- Tăng cân (`Gain`) cộng mức điều chỉnh đã xác nhận vào TDEE. Trần là `min(500, 20% × TDEE)`. BMI hiện tại phải dưới 25 và BMI đích tối đa 24,9 theo chính sách ứng dụng.
- Khi tạo mục tiêu giảm hoặc tăng, chênh lệch cân phải từ 1 kg và thời hạn từ 2 tuần. Duy trì có thể chọn 1 tuần. Các ngưỡng năng lượng và quy tắc lập kế hoạch nằm trong `GoalPolicy` và `GoalPlanningService`.

### 4.5. Cân bằng Năng lượng & Quy đổi Khẩu phần
- Quy đổi món ăn: `Dinh dưỡng thực tế = Dinh dưỡng chuẩn * (Servings nhập / ServingSize chuẩn)`
- Dùng `GoalPolicy.Today` cho ngày nghiệp vụ Việt Nam. Các mốc kỹ thuật tiếp tục lưu UTC.
- `RemainingCalories = DailyCalorieTarget - CaloriesConsumedToday + ExerciseCredit`
- `ExerciseCredit` bằng 50% tổng calo vận động đã ghi trong ngày, làm tròn và giới hạn 300 kcal. Bản ghi vận động mới tính phần tiêu hao trên mức nghỉ theo `max(MET - 1, 0)`.
- Hiển thị màu trên Dashboard:
  - `RemainingCalories > 50`: Xanh lá (Còn ngân sách calo)
  - `-50 <= RemainingCalories <= 50`: Vàng (Đạt sát mục tiêu)
  - `RemainingCalories < -50`: Đỏ (Đã vượt calo mục tiêu)

### 4.6. Thuật toán Gợi ý Thực đơn Cá nhân hóa
- Phân bổ ngân sách calo theo bữa:
  - Nếu 4 bữa: Sáng `25%`, Trưa `35%`, Tối `30%`, Phụ `10%`.
  - Nếu 3 bữa: Sáng `30%`, Trưa `40%`, Tối `30%`.
- Bộ lọc cứng: Loại bỏ toàn bộ món có `IsHidden == true`, món chứa thành phần nằm trong `Allergies` hoặc `DislikedFoods` của User, và món vi phạm `DietType` (ví dụ `Vegetarian` thì loại bỏ thịt/cá/hải sản).
- Chọn tổ hợp món cho từng bữa sao cho tổng calo toàn ngày nằm trong khoảng `±10%` so với `DailyCalorieTarget`.
- Cho phép đổi món (`Swap Food`) cùng nhóm thực phẩm (`CategoryId`) với độ lệch calo trong khoảng `±15%`.

## 5. Quy tắc Bảo mật & Quyền Riêng tư (Bắt buộc Tuân thủ)
1. **Chống lỗi IDOR (Insecure Direct Object Reference)**:
   - Mọi Action trong Controller của User (`Profile`, `Goal`, `WeightLog`, `MealLog`, `WaterLog`, `ExerciseLog`, `Favorite`, `Chat`) bắt buộc lấy `userId` từ `User.FindFirstValue(ClaimTypes.NameIdentifier)`.
   - Tuyệt đối không nhận `userId` từ Query String, Route hay Form Body để truy vấn hoặc cập nhật dữ liệu. Khi xóa/sửa bản ghi (`weight_logs`, `meal_log_details`, `chat_sessions`...), câu lệnh EF Core luôn phải có điều kiện `.Where(x => x.Id == id && x.UserId == currentUserId)`.
2. **Giới hạn quyền của Admin**:
   - Admin chỉ được xem thông tin tài khoản cơ bản (`FullName`, `Email`, `Role`, `IsLocked`, `CreatedAt`, `LastLoginAt`) và thống kê tổng hợp toàn hệ thống.
   - Không xây dựng màn hình hay API cho phép Admin xem chi tiết hay chỉnh sửa nhật ký cân nặng, bữa ăn, nước uống hoặc nội dung tin nhắn chat cá nhân của từng User.
3. **Validation Đầu vào**:
   - Chiều cao: `100 – 250 cm`
   - Cân nặng: `20 – 300 kg` (1 chữ số thập phân)
   - Tuổi: `13 – 100 tuổi`
   - Lượng nước mỗi lần nhập: `50 – 2000 ml`
   - Thời gian vận động: `1 – 300 phút`
   - Ảnh tải lên: Chỉ chấp nhận `.jpg`, `.jpeg`, `.png`, `.webp`, kích thước tối đa `5 MB`.

## 6. Quy tắc Thiết kế Chatbot AI
1. **Kiểm tra An toàn Y tế trước tiên (`ChatSafetyFilter`)**:
   - Trước khi gọi Gemini API, kiểm tra câu hỏi của người dùng với danh sách từ khóa/ý định nhạy cảm: *đau ngực, khó thở, ngất xỉu, rối loạn ăn uống, tự gây nôn, nhịn ăn cực đoan, thuốc giảm cân, thuốc kê đơn, инсулин, tiểu đường type 1, suy thận, mang thai, dị ứng phản vệ, sụt cân bất thường*.
   - Nếu phát hiện rủi ro y tế: Không tư vấn điều trị hay kê thuốc; trả về phản hồi cảnh báo chuẩn khuyên gặp bác sĩ/chuyên gia dinh dưỡng lâm sàng.
2. **Chống Hallucination (Không bịa số liệu)**:
   - Khi người dùng hỏi về calo còn lại hôm nay, tổng protein đã nạp, hoặc tra cứu/gợi ý món ăn, Chatbot bắt buộc lấy số liệu từ `UserContextBuilder` và `FoodToolService` (truy vấn trực tiếp CSDL). Nếu món ăn không có trong bảng `foods`, phải trả lời rõ là "Hiện chưa có dữ liệu chính xác của món này trong danh mục hệ thống" trước khi đưa ra ước lượng tham khảo.
