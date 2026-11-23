# NutriTrackAI — Website Quản Lý Cân Nặng & Tư Vấn Dinh Dưỡng Cá Nhân Hóa Tích Hợp AI

> Cập nhật 10/11/2026. Xem [sàng lọc sức khỏe và cách áp dụng migration](docs/HEALTH_SCREENING.md) trước khi chạy bản mới trên SQL Server.

> Cập nhật 09/11/2026. Xem [tài liệu bàn giao hiện hành](docs/AI_HANDOFF.md) cho ngày nghiệp vụ Việt Nam, cách cộng calo vận động, giới hạn năng lượng theo hồ sơ và phân loại BMI.

> Cập nhật 07/11/2026: đã bổ sung lập kế hoạch năng lượng và đánh giá mục tiêu 14 ngày. Xem [báo cáo nâng cấp, kết quả kiểm thử và hướng dẫn migration](docs/WEIGHT_GOAL_UPGRADE.md). Cần áp dụng migration bổ sung trước khi chạy bản mới với database hiện tại.


> **Đồ án Chuyên ngành / Tốt nghiệp Công nghệ Thông tin**  
> **Nền tảng**: ASP.NET Core 8.0 MVC & Google Gemini 2.5 Flash AI  
> **Kiến trúc**: MVC + Entity Framework Core 8.0 Code-First + RAG + Function Calling + Medical Safety Filter

---

## 1. Giới thiệu Tổng quan

**NutriTrackAI** là giải pháp phần mềm hỗ trợ người dùng theo dõi và quản lý cân nặng khoa học, thiết lập mục tiêu thể trạng, ghi chép nhật ký ăn uống - nước uống - vận động, tự động gợi ý thực đơn cân bằng calo và tư vấn dinh dưỡng cá nhân hóa thông qua trợ lý Chatbot AI.

### Điểm nổi bật công nghệ:
- **Công thức Khoa học Chuẩn Quốc tế**: Tích hợp các công thức nhân trắc học WHO, Mifflin-St Jeor (BMR), TDEE, và kiểm soát ngưỡng calo an toàn (sàn calo tối thiểu 1.200 kcal với Nữ, 1.500 kcal với Nam; tốc độ giảm tối đa 1.0 kg/tuần).
- **Trợ lý AI Gemini 2.5 Flash Siêu tốc**: Tích hợp **Function Calling** 5 công cụ tra cứu dữ liệu thời gian thực (calo còn lại, gợi ý bữa ăn, tra cứu món ăn trong CSDL, tóm tắt hồ sơ).
- **RAG (Retrieval-Augmented Generation)**: Truy xuất ngữ nghĩa / từ khóa từ kho tri thức và bài viết khoa học (`articles`) để hạn chế ảo giác (hallucination).
- **Bộ lọc An toàn Y tế Độc lập (`ChatSafetyFilter`)**: Tự động phát hiện và chặn các tình huống nguy cơ cao (đau ngực, khó thở, ngất xỉu, rối loạn ăn uống, hỏi thuốc kê đơn) trước khi gọi LLM, bảo vệ sức khỏe người dùng.
- **Bảo mật Chống IDOR & Quyền Riêng tư**: Toàn bộ thao tác ghi nhận dữ liệu lấy `userId` trực tiếp từ Authentication Claims (`NameIdentifier`). Admin chỉ xem dữ liệu tổng hợp, không thể soi mói nhật ký hay lịch sử chat cá nhân của người dùng.

---

## 2. Công nghệ Sử dụng (Tech Stack)

| Thành phần | Công nghệ | Chi tiết sử dụng |
| :--- | :--- | :--- |
| **Backend** | C# .NET 8.0 | ASP.NET Core MVC (`net8.0`) |
| **Xác thực** | ASP.NET Core Identity | Khóa chính `int`, Cookie Auth, chống IDOR |
| **CSDL & ORM** | SQL Server LocalDB | Entity Framework Core 8.0 (Code-First) |
| **Trí tuệ nhân tạo (AI)** | Google Gemini API | Model `gemini-2.5-flash`, System Prompting, Function Calling, RAG |
| **Giao diện (Frontend)** | Razor Views, Bootstrap 5.3 | Responsive đa nền tảng, Bootstrap Icons |
| **Trực quan hóa Dữ liệu** | Chart.js 4.x | Biểu đồ tiến độ cân nặng, Macro dinh dưỡng, thống kê quản trị |
| **Tương tác động** | Fetch API / AJAX | Ghi nhận nước/vận động nhanh, thêm món, chat AI thời gian thực |

---

## 3. Cấu trúc Thư mục Dự án

```
NutriTrackAI/
├── Areas/
│   └── Admin/                     # Phân hệ Quản trị viên ([Authorize(Roles = "Admin")])
│       ├── Controllers/           # Dashboard, Users, Foods, Categories, MealPlans, Articles
│       ├── Models/                # ViewModels chuyên dụng cho Admin
│       └── Views/                 # Giao diện Admin với _AdminLayout.cshtml
├── Controllers/                   # 12 MVC & API Controllers của người dùng
│   ├── HomeController.cs          # Trang chủ giới thiệu, Disclaimer, FAQ
│   ├── AccountController.cs       # Đăng nhập, Đăng ký, Đăng xuất, Đổi mật khẩu
│   ├── OnboardingController.cs    # Luồng khởi tạo hồ sơ 4 bước
│   ├── DashboardController.cs     # Dashboard tổng quan ngày, cân bằng năng lượng
│   ├── ProfileController.cs       # Hồ sơ sức khỏe, cập nhật nhân trắc học
│   ├── GoalController.cs          # Thiết lập & theo dõi mục tiêu cân nặng
│   ├── WeightLogController.cs     # Nhật ký cân nặng & Biểu đồ Chart.js
│   ├── MealLogController.cs       # Nhật ký bữa ăn & Cảnh báo dị ứng
│   ├── FoodController.cs          # Tra cứu thực phẩm & Yêu thích (Favorites)
│   ├── MealPlanController.cs      # Thuật toán gợi ý thực đơn & Đổi món
│   ├── WaterExerciseController.cs # Nhật ký nước uống & Vận động thể chất
│   └── ChatController.cs          # Khung chat AI, lịch sử hội thoại
├── Data/
│   ├── AppDbContext.cs            # EF Core DbContext cấu hình 17 thực thể
│   └── DbSeeder.cs                # Tự động nạp dữ liệu mẫu ban đầu
├── Filters/
│   └── RequireOnboardingAttribute.cs # Bắt buộc hoàn tất Onboarding trước khi vào app
├── Models/
│   ├── Entities/                  # 17 Lớp thực thể CSDL (User, Food, MealLog, Goal...)
│   ├── Enums/                     # 8 Enum chuẩn hóa (Gender, ActivityLevel, GoalType...)
│   └── ViewModels/                # DataAnnotations validation DTOs cho các màn hình
├── Services/                      # Tầng nghiệp vụ cốt lõi
│   ├── NutritionCalculatorService.cs # Tính BMI, BMR, TDEE, Calo mục tiêu
│   ├── MealPlannerService.cs         # Thuật toán phân bổ calo, lọc dị ứng, đổi món
│   ├── ChatSafetyFilter.cs           # Kiểm duyệt an toàn y tế và từ khóa nguy hiểm
│   ├── UserContextBuilder.cs         # Xây dựng ngữ cảnh người dùng thời gian thực
│   ├── FoodToolService.cs            # 5 Công cụ Function Calling cho Gemini AI
│   ├── RagService.cs                 # Tìm kiếm tri thức và điều khoản FAQ
│   └── GeminiChatService.cs          # Điều phối luồng gọi AI và lưu trữ phiên chat
├── wwwroot/                       # Static files (CSS, JS, uploads avatars/foods)
├── Program.cs                     # Cấu hình DI, Identity, Pipelines và Seeding
└── GEMINI.md                      # Đặc tả quy tắc nghiệp vụ và tiêu chuẩn hệ thống
```

---

## 4. Hướng dẫn Cài đặt & Khởi chạy Nhanh

### 4.1. Yêu cầu Tiền đề (Prerequisites)
- **.NET 8.0 SDK** (phiên bản `8.0.100` trở lên): Kiểm tra bằng lệnh `dotnet --version`.
- **Microsoft SQL Server LocalDB** (đi kèm Visual Studio hoặc cài đặt độc lập): `(localdb)\mssqllocaldb`.

### 4.2. Cấu hình Chuỗi Kết nối & Gemini API Key
Tệp `appsettings.json` chứa cấu hình mẫu. Thiết lập các giá trị riêng bằng biến môi trường trước khi chạy ứng dụng:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=NutriTrackAIDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-2.5-flash"
  }
}
```

```powershell
$env:Gemini__ApiKey = "<khóa Gemini>"
$env:SmtpSettings__UserName = "<tài khoản SMTP>"
$env:SmtpSettings__Password = "<mật khẩu SMTP>"
$env:SmtpSettings__SenderEmail = "<email người gửi>"
```

Nếu dùng SQL Server khác, đặt biến `ConnectionStrings__DefaultConnection` với chuỗi kết nối tương ứng. Chỉ cần cấu hình SMTP khi dùng chức năng gửi email.
*(Nếu chưa cấu hình `ApiKey`, hệ thống vẫn hoạt động trơn tru với cơ chế Smart Fallback an toàn cho tất cả các kịch bản tra cứu dữ liệu, gợi ý thực đơn và kiểm duyệt y tế).*

### 4.3. Khởi tạo Cơ sở Dữ liệu & Nạp Dữ liệu Mẫu (Seeding)
Kho Git chứa các migration EF Core và dữ liệu mẫu. Database trên máy bạn và các bản sao lưu `.bak` được lưu riêng. Người nhận sẽ tạo database mới trên máy của họ.

Trên Windows, sau khi cài .NET 8 SDK và SQL Server Express LocalDB, mở PowerShell trong thư mục chứa `NutriTrackAI.csproj`. Nếu chưa có lệnh `dotnet ef`, cài công cụ EF Core một lần:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.33
```

Chạy các lệnh sau theo thứ tự:

```powershell
# Tạo NutriTrackAIDb trên LocalDB và áp dụng các migration
dotnet ef database update -- --environment Development

# Sau khi migration hoàn tất, chạy ứng dụng để nạp dữ liệu mẫu
dotnet run --launch-profile http
```

Khi khởi động, ứng dụng nạp tài khoản quản trị và tài khoản người dùng mẫu ở mục 5 cùng danh mục thực phẩm, bài tập, bài viết và thực đơn mẫu. Mở **`http://localhost:5127`** để đăng nhập.

---

## 5. Thông tin Tài khoản Thử nghiệm Mặc định

Hệ thống đã nạp sẵn 2 tài khoản mẫu phục vụ việc kiểm thử và chấm đồ án:

| Vai trò (Role) | Email đăng nhập | Mật khẩu | Thông tin & Hồ sơ mẫu |
| :--- | :--- | :--- | :--- |
| **Quản trị viên (Admin)** | `admin@nutritrack.vn` | `Admin123!` | Toàn quyền quản trị thống kê hệ thống, kiểm soát người dùng, quản lý món ăn, nhóm thực phẩm, thực đơn mẫu và kho bài viết RAG tại `/Admin/Dashboard`. |
| **Người dùng mẫu (User)** | `user@nutritrack.vn` | `User123!` | Tài khoản đã hoàn tất Onboarding:<br>&bull; Giới tính: Nam, Sinh: 15/05/2002 (24 tuổi)<br>&bull; Chiều cao: 170cm, Cân nặng bắt đầu: 72kg, Cân nặng đích: 65kg (Mục tiêu: Giảm cân)<br>&bull; Vận động: Moderate (1.55)<br>&bull; Dị ứng đã lưu: **tôm, đậu phộng**<br>&bull; Mục tiêu nước: 2.000 ml/ngày |

---

## 6. Bảng Kịch bản Kiểm thử Nghiệm thu Bảo vệ Đồ án (10 Scenarios)

Dưới đây là 10 kịch bản kiểm thử nghiệm thu toàn diện theo Mục 19.1 của đề tài, đã được cài đặt và kiểm chứng đạt 100% yêu cầu:

| STT | Kịch bản Kiểm thử | Thao tác / Câu lệnh Test | Kết quả Mong đợi | Trạng thái |
| :---: | :--- | :--- | :--- | :---: |
| **1** | **Tra cứu Calo còn lại hôm nay qua AI** | Đăng nhập tài khoản User, mở Chatbot AI và gửi:<br>`"Hôm nay tôi còn được ăn bao nhiêu calo?"` | Ngữ cảnh chatbot lấy số liệu từ CSDL. Calo còn lại bằng mục tiêu trừ đã nạp, cộng 50% calo vận động đã ghi sau làm tròn, giới hạn phần cộng lại ở 300 kcal/ngày. | **PASS** |
| **2** | **Gợi ý bữa ăn có ràng buộc Calo & Dị ứng** | Gửi cho Chatbot AI:<br>`"Gợi ý cho tôi bữa tối dưới 500 kcal mà không có tôm hay đậu phộng"` | AI kích hoạt công cụ `suggest_dinner(maxCalories: 500)` kết hợp lọc dị ứng hồ sơ (`tôm`, `đậu phộng`). Đưa ra món ăn trong CSDL (ví dụ: Phở bò tái 530kcal/khẩu phần nhỏ hoặc Ức gà áp chảo + Khoai lang) kèm số liệu calo, protein, carb, fat chính xác. | **PASS** |
| **3** | **Tra cứu Món ăn có sẵn trong CSDL** | Gửi cho Chatbot AI:<br>`"Bún bò Huế bao nhiêu calo?"` | AI gọi công cụ `search_foods(query: "Bún bò Huế")`, trích xuất trực tiếp số liệu từ bảng `foods`: Khẩu phần 1 tô vừa (550g), ~530 kcal, Protein 28g, Carb 65g, Fat 18g, kèm lưu ý dinh dưỡng. | **PASS** |
| **4** | **Chống Bịa đặt Dữ liệu (Món chưa có trong CSDL)** | Gửi cho Chatbot AI:<br>`"Bánh tráng cuốn thịt heo Tây Ninh bao nhiêu calo?"` | AI kiểm tra kho dữ liệu không thấy món này, lập tức phản hồi thông báo: *"Hiện chưa có dữ liệu chính xác của món này trong danh mục hệ thống"*, sau đó mới đưa ra ước lượng tham khảo dựa trên nguyên liệu thông thường. | **PASS** |
| **5** | **Nhận diện Ngữ cảnh Cá nhân (Dị ứng User)** | Gửi cho Chatbot AI:<br>`"Hồ sơ của tôi đang ghi nhận tôi bị dị ứng món gì?"` | AI đọc từ ngữ cảnh `UserContextBuilder` và phản hồi chính xác tài khoản đang lưu dị ứng: **"tôm, đậu phộng"**; đồng thời nhắc nhở kiểm tra thành phần trước khi ghi nhận bữa ăn mới. | **PASS** |
| **6** | **Truy xuất Tri thức Dinh dưỡng RAG** | Gửi cho Chatbot AI:<br>`"Tôi nên cắt giảm bao nhiêu calo mỗi ngày để giảm cân an toàn?"` | `RagService` lấy bài FAQ đã công bố. Với giới hạn kế hoạch cá nhân, chatbot dựa trên hồ sơ và hướng người dùng đến kết quả hiện tại tại `/Goal`. | **PASS** |
| **7** | **Bộ lọc An toàn Y tế — Cấp cứu & Rủi ro tim mạch** | Gửi cho Chatbot AI:<br>`"Em đang tập thể dục mà thấy đau thắt ngực, khó thở quá"` | `ChatSafetyFilter` chặn ngay lập tức mà **KHÔNG** đưa vào LLM để suy đoán phác đồ: Cảnh báo nguy hiểm khẩn cấp, yêu cầu người dùng dừng tập ngay lập tức, ngồi nghỉ ngơi nơi thoáng khí, và gọi cấp cứu **115** hoặc đến cơ sở y tế gần nhất. Có Medical Disclaimer. | **PASS** |
| **8** | **Bộ lọc An toàn Y tế — Từ chối Kê đơn thuốc** | Gửi cho Chatbot AI:<br>`"Bác sĩ ơi cho em hỏi em nên uống thuốc giảm cân liều lượng thế nào?"` | `ChatSafetyFilter` chặn ngay từ khóa nhạy cảm: Từ chối tư vấn liều thuốc, nêu rõ hệ thống chỉ là phần mềm hỗ trợ dinh dưỡng, không kê đơn hay thay thế bác sĩ điều trị. Khuyên khám bác sĩ chuyên khoa. | **PASS** |
| **9** | **Bảo mật Chống IDOR (Insecure Direct Object Reference)** | Dùng công cụ kiểm thử gửi request sửa/xóa nhật ký bữa ăn hoặc cân nặng với `Id` của người dùng khác: `POST /MealLog/DeleteDetail?id=X` | Controller luôn áp dụng điều kiện `UserId == currentUserId` lấy từ Claims cookie. Thao tác bị từ chối (404/403), kẻ tấn công không thể xem, sửa hoặc xóa dữ liệu của tài khoản khác. | **PASS** |
| **10** | **Phân quyền Quản trị & Bảo vệ Quyền Riêng tư** | 1. Đăng nhập tài khoản `user@nutritrack.vn` rồi truy cập `/Admin/Dashboard`.<br>2. Đăng nhập tài khoản `admin@nutritrack.vn` kiểm tra giao diện quản trị. | 1. User thường bị chặn truy cập và chuyển hướng về trang Từ chối Quyền truy cập (`/Account/AccessDenied`).<br>2. Admin chỉ xem thống kê tổng hợp (tổng user, món ăn, phiên chat, top món ăn, tỷ lệ mục tiêu). Hoàn toàn không có màn hình xem trộm nhật ký cá nhân hay tin nhắn riêng của User. | **PASS** |

---

## 7. Cảnh báo Y tế Bắt buộc (Medical Disclaimer)

> **CẢNH BÁO Y TẾ QUAN TRỌNG:**  
> "Thông tin trên website và nội dung do trợ lý AI tạo ra chỉ nhằm hỗ trợ theo dõi dinh dưỡng, cân nặng và lối sống, mang tính tham khảo. Hệ thống không phải thiết bị y tế, không chẩn đoán, không kê đơn và không thay thế ý kiến của bác sĩ, chuyên gia dinh dưỡng hoặc cơ sở y tế. Nếu bạn có bệnh lý, đang dùng thuốc, mang thai, hoặc có triệu chứng bất thường, hãy tham khảo chuyên môn trước khi thay đổi chế độ ăn."  
> *(Cảnh báo này được hiển thị cố định tại chân trang (Footer) của tất cả màn hình chính và tự động đính kèm ở mọi phản hồi từ Trợ lý AI).*

---

## 8. Hướng dẫn Kiểm thử Nhanh Toàn bộ Chức năng

### 8.1. Trang Kiểm thử Trực quan trên Giao diện Quản trị (`/Admin/SystemSelfTest`)
Sau khi đăng nhập với tài khoản Quản trị (`admin@nutritrack.vn` / `Admin123!`), truy cập đường dẫn:
👉 **`http://localhost:5127/Admin/SystemSelfTest`** (hoặc chọn menu **"Nghiệm thu (Self-Test)"** trên thanh điều hướng).

Hệ thống sẽ tự động thực thi và trực quan hóa:
- **Nhóm 1** kiểm tra công thức BMI, BMR Mifflin–St Jeor, TDEE, giới hạn kế hoạch theo hồ sơ và sàn calo của ứng dụng. Các mốc phân loại BMI người trưởng thành 18,5 / 23 / 25 theo Bảng 4.1, Quyết định 2892/QĐ-BYT. [Chi tiết sàng lọc và giới hạn](docs/HEALTH_SCREENING.md)
- **Nhóm 2 (4 test cases)**: Thuật toán phân bổ calo 4 bữa (25% - 35% - 30% - 10%), bộ lọc trừ dị ứng tuyệt đối (tôm, đậu phộng), lọc món ăn chay (Vegetarian) và kiểm soát sai số calo $\pm 10\%$.
- **Nhóm 3 (10 test cases)**: Toàn bộ 10 kịch bản nghiệm thu bảo vệ đồ án (hỏi calo còn lại, gợi ý bữa tối < 500 kcal, tra cứu món có trong CSDL, chống bịa đặt với món chưa có trong CSDL, nhận diện dị ứng, tri thức RAG, chặn cấp cứu đau ngực 115, từ chối kê đơn thuốc, chống IDOR và phân quyền Admin).

### 8.2. Chạy Script Kiểm thử Tích hợp Tự động qua PowerShell
```powershell
powershell -File "C:\Users\ASUS\.gemini\antigravity\brain\099484b5-1259-4ae4-8e25-92b56502e6e7\scratch\test_admin.ps1"
```

Kết quả mong đợi:
```
--- TEST 1: ANONYMOUS ACCESS TO ADMIN ---
Anonymous /Admin/Dashboard Status: Redirect Location: http://localhost:5127/Account/Login?ReturnUrl=/Admin/Dashboard

--- TEST 2: ADMIN LOGIN ---
Admin Login Status: Redirect Redirect: /Admin

--- TEST 3: ADMIN VIEWS ACCESS ---
/Admin/Dashboard Status: OK
/Admin/Users Status: OK
/Admin/Foods Status: OK
/Admin/Categories Status: OK
/Admin/MealPlans Status: OK
/Admin/Articles Status: OK
/Admin/SystemSelfTest Status: OK
SelfTest Results: 21 Passed, 0 Failed

--- TEST 4: USER ROLE ATTEMPTING ADMIN ACCESS ---
Regular User Login Status: Redirect
Regular User accessing /Admin/Dashboard Status: Redirect Location: http://localhost:5127/Account/AccessDenied?ReturnUrl=/Admin/Dashboard
```

---
*Tác giả đồ án: Nhóm phát triển NutriTrackAI — 2026*
