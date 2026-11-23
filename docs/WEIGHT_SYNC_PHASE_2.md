# Đặc tả Nhóm 1 — Đồng bộ cân nặng giữa Hồ sơ và Nhật ký

## Phạm vi

Triển khai Nhóm 1 của `CODEX_PROMPT_2.md` trong dự án hiện tại. Dùng ngày nghiệp vụ `GoalPolicy.Today`. Giữ route hiện có, quyền sở hữu, antiforgery, chính sách mục tiêu, sàng lọc, snapshot bữa ăn và cấu trúc dữ liệu hiện tại. Xác minh bằng cơ sở dữ liệu giả lập. Nhóm 2 và Nhóm 3 thuộc đợt triển khai riêng.

## Quy tắc ghi và đọc

- Nhật ký là nguồn của cân đã ghi và xu hướng. Sắp xếp theo `LoggedOn DESC, Id DESC` để chọn mốc mới nhất. `UserProfile.CurrentWeightKg` tiếp tục là đầu vào của kế hoạch calo. Sau khi ghi mốc mới nhất, hoặc sửa/xóa mốc khiến cân hiện tại thay đổi, cập nhật hồ sơ từ mốc mới nhất còn lại. Khi xóa mốc cuối cùng, giữ cân hồ sơ làm mốc ban đầu. Mọi bước ghi nhật ký, đồng bộ hồ sơ và cập nhật ngân sách nằm trong cùng giao dịch.
- Hồ sơ chỉ xử lý cân khi người dùng nhập giá trị khác cân đang lưu ở Hồ sơ. Nếu người dùng nhập đúng cân đã có ở mốc mới nhất, đồng bộ hồ sơ với mốc đó và giữ dữ liệu nhật ký. Nếu nhập cân mới, tạo hoặc cập nhật mốc hôm nay với ghi chú **Cập nhật từ Hồ sơ**. Lưu trường khác khi dữ liệu đang lệch giữ nguyên cảnh báo và hai giá trị.
- Thêm từ Nhật ký cập nhật mốc của ngày đã có. Sửa theo ID giữ bản ghi và kiểm tra chủ sở hữu. Nếu chuyển sang ngày đã có bản ghi khác, yêu cầu người dùng chọn bản ghi cần sửa. Khi sửa hoặc xóa mốc mới nhất, tính lại mốc mới nhất. Sửa/xóa mốc cũ không tự hòa giải sai lệch di sản. Giữ ảnh khi người dùng chưa chọn ảnh mới. Onboarding sử dụng cùng nghiệp vụ tạo/cập nhật mốc hôm nay.
- Dashboard dùng cân đã ghi cho tiến độ và biểu đồ. Nếu cửa sổ 30 ngày trống nhưng có lịch sử, hiển thị mốc gần nhất với ngày thật. Nếu chưa từng ghi cân, có thể dùng cân hồ sơ làm mốc ban đầu. Thẻ Nhật ký tính tổng biến động từ mốc đầu và cuối của dữ liệu đã ghi. Khi dữ liệu lệch, ngân sách vẫn dùng cân hồ sơ cho tới khi người dùng cập nhật cân hiện tại hoặc thực hiện hòa giải.

## Xác nhận thay đổi lớn và hòa giải

- Yêu cầu xác nhận khi độ lệch **lớn hơn** `max(2,0 kg, 3% × cân tham chiếu)` và mốc tham chiếu cách ngày đang ghi từ 0 đến 14 ngày, tính cả hai biên. Dùng mốc đang sửa/cập nhật nếu ngày giữ nguyên. Khi ghi bù hoặc đổi ngày, dùng mốc tại hoặc trước ngày ghi và bỏ qua chính bản ghi đang di chuyển. Nếu thiếu mốc tham chiếu có ngày thì bỏ qua kiểm tra độ lệch. Báo lỗi trên biểu mẫu, giữ số cân/ngày/ghi chú và chờ người dùng xác nhận trước khi lưu. Ảnh mới phải chọn lại khi trình duyệt tải lại biểu mẫu.
- Hiện cảnh báo tách biệt trên Dashboard, Hồ sơ và Cân nặng khi cân hồ sơ và mốc nhật ký mới nhất khác nhau. Hai lựa chọn **Ghi nhận cân hồ sơ cho hôm nay** và **Dùng cân nhật ký** là thao tác xác nhận rõ ràng. Hai POST kiểm tra antiforgery, lấy người dùng từ phiên đăng nhập, đối chiếu lại cân hồ sơ, cân và ngày nhật ký với nội dung đã hiển thị. Nếu dữ liệu đổi, yêu cầu tải lại. Chỉ chuyển hướng về Dashboard, Hồ sơ hoặc Cân nặng. Thông báo kết quả và cập nhật ngân sách sau lựa chọn.

## Xác minh

Kiểm tra qua HTTP với dữ liệu giả lập cho cập nhật cân trong Hồ sơ, gửi lặp trong cùng ngày, lưu trường khác khi dữ liệu lệch, xác nhận thay đổi lớn ở cả hai màn hình, sửa và xóa mốc mới nhất, hai lựa chọn hòa giải, dữ liệu đã thay đổi, quyền sở hữu, antiforgery và biểu đồ khi mốc cuối đã quá 30 ngày. InMemory xác nhận hành vi ứng dụng. Kiểm tra giao dịch dưới tải đồng thời của SQL Server thuộc môi trường test SQL riêng.

Đã build và chạy `dotnet test NutriTrackAI.Tests/NutriTrackAI.Tests.csproj -c WeightSyncPhase2 --no-restore -p:UseSharedCompilation=false` với kết quả **148/148 bài kiểm thử đạt**. Có cảnh báo NuGet `NU1900` do không tải được dữ liệu kiểm tra lỗ hổng của gói. Kiểm tra HTML qua HTTP đã chạy trên host QA. Phiên kiểm tra giao diện bằng trình duyệt không khởi tạo được. Chưa chạy thử SQL Server/LocalDB.
