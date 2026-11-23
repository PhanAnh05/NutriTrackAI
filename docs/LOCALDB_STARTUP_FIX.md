# Sửa kết nối LocalDB khi khởi động — 07/11/2026

## Kết quả

Đã kiểm tra khởi động từ trạng thái LocalDB Stopped, dùng đúng profile https trong Properties/launchSettings.json. Bản Debug tự khởi động instance MSSQLLocalDB qua công cụ được cài trên máy, lấy named pipe mới, đọc migration history, hoàn tất startup và trả HTTP 200 tại https://localhost:7212/Account/Login. Tiến trình kiểm tra được dừng sau xác minh để không chiếm cổng khi F5 trong Visual Studio.

82/82 test Passed, 0 Failed. Build Debug: 0 warnings, 0 errors. Dữ liệu test2 (UserId 1004) kiểm tra lại vẫn là 71.4 kg, mục tiêu 70 kg, ngân sách 2209 kcal/ngày, ngày bắt đầu 24/10/2026, ngày mong muốn 05/12/2026, 15 bản ghi cân gồm mốc khởi tạo và 14 ngày demo. Không reset, tạo lại hoặc xóa database.

## Nguyên nhân tái diễn

Bản resolver trước chỉ lấy pipe của instance đang Running. Khi instance Stopped và không có pipe, nó trả lại chuỗi (localdb) ban đầu; SqlClient lại gọi cơ chế automatic-start đang báo lỗi Windows 575 / WaitForMultipleObjects / 0x89c5010a. Truy vấn GetPendingMigrationsAsync ở Program.cs chỉ là lần đầu cần kết nối, không phải nguyên nhân làm SQL Server không khởi động.

Không xác định được nguyên nhân nội bộ của lỗi Windows chỉ từ log; không kết luận database hỏng hoặc lỗi dữ liệu demo. Điểm được xác minh là lệnh SqlLocalDB start hoạt động trong ngữ cảnh kiểm tra, còn automatic-start có lần thất bại.

## Luồng sửa hiện tại

1. Chỉ áp dụng trên Windows, môi trường Development và Data Source dạng (localdb)\<instance>.
2. Gọi công cụ SqlLocalDB.exe trong thư mục cài SQL Server, với lệnh info và tên instance cấu hình.
3. Nếu có pipe: dùng pipe đó. Nếu chưa có: gọi start cho đúng instance, rồi gọi info LẦN NỮA để lấy pipe mới.
4. Giữ nguyên tên database, xác thực Windows và các tùy chọn kết nối khác; không lưu cứng tên pipe.
5. Nếu start thất bại, timeout hoặc vẫn không có pipe: báo lỗi cụ thể, không quay về kết nối automatic-start đã biết đang thất bại.
6. Production, SQL Server không phải LocalDB và máy không phải Windows giữ nguyên cấu hình. Các tests kiểm tra các nhánh này không gọi công cụ LocalDB.

File sửa: Data/LocalDbDevelopmentConnection.cs. Tests bổ sung: NutriTrackAI.Tests/LocalDbConnectionTests.cs. Program.cs vẫn gọi resolver trước đăng ký DbContext như bản sửa trước.

## Bằng chứng chạy thật

- Dừng bằng SqlLocalDB stop MSSQLLocalDB thông thường, không force; info xác nhận Stopped.
- Build Debug thành công.
- Chạy dotnet run --project NutriTrackAI.csproj --no-build --launch-profile https.
- LocalDB Last start time đổi, pipe mới được tạo, ứng dụng đọc __EFMigrationsHistory và khởi động trên 7212/5127.
- Invoke-WebRequest đến HTTPS 7212/Account/Login trả 200, trang đăng nhập đúng. Không bỏ kiểm tra chứng chỉ trong request kiểm tra.
- Query chỉ đọc xác nhận dữ liệu test2 được giữ nguyên.

Các unit test chạy bằng dotnet test NutriTrackAI.Tests/NutriTrackAI.Tests.csproj -c LocalDbFix --no-restore -p:UseSharedCompilation=false, file kết quả NutriTrackAI.Tests/TestResults/localdb-fix.trx.

Công cụ điều khiển cửa sổ không khởi tạo được trong phiên này, nên không tuyên bố đã bấm F5 trực tiếp trong Visual Studio. Kiểm chứng runtime dùng chính profile https mà Visual Studio đang chọn, không chỉ host QA InMemory.

## Chạy tiếp

Dừng phiên debug cũ rồi F5 trong Visual Studio với profile https hoặc http. Không tạo lại tài khoản hay đổi database. Nếu công cụ LocalDB thực sự không khởi động được trong phiên Windows của bạn, thông báo mới sẽ nêu lỗi của SqlLocalDB start thay vì che lỗi hoặc tự tạo database khác.

Nguồn Microsoft: [SqlLocalDB utility](https://learn.microsoft.com/en-us/sql/tools/sqllocaldb-utility) mô tả info/start; [SQL Server Express LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) hướng dẫn kết nối bằng instance pipe name. Không thay đổi Windows security, driver, cấu hình mã hóa hoặc quyền database.
