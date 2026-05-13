---
name: skilllist
description: Cung cấp toàn bộ ngữ cảnh hệ thống, kiến trúc và quy trình nghiệp vụ cho AI Agent để code đồng nhất dự án STEM.
---

# 🛠 TỔNG QUAN HỆ THỐNG (SYSTEM OVERVIEW)

- **Tên dự án:** STEM (QPSTEM)
- **Mục tiêu cốt lõi:** Nền tảng quản lý trung tâm STEM gồm: landing public, quản lý khóa/lớp/buổi học, điểm danh minh chứng (note + media), mượn/trả thiết bị, học phí & công nợ, và backoffice vận hành.
- **Đối tượng người dùng:**
  - **Public:** phụ huynh/học sinh/khách truy cập xem khóa học & tin tức
  - **Student (Học sinh):** xem lịch, điểm danh/nhận xét, minh chứng/media, hóa đơn công nợ
  - **Teacher (Giáo viên):** điểm danh theo buổi, nhập ghi chú & media, phân tích AI cho hỗ trợ ghi chú, mượn/trả thiết bị, xem lịch dạy
  - **Admin (Quản trị):** dashboard KPI, quản lý người dùng, cấu hình nội dung/khóa/lớp/buổi, quản lý tài chính & kho

## Luật “AI nhúng vào form nghiệp vụ”

- AI không nằm như một menu riêng.
- AI được nhúng trực tiếp vào các form thao tác (đặc biệt màn teacher trong `Attendances/Board`): giáo viên có thể nhấn nút AI viết lại ghi chú hoặc phân tích video rồi “chép kết quả” vào textarea trước khi lưu.

# 🏗 KIẾN TRÚC KỸ THUẬT (TECH STACK)

| Thành phần    | Công nghệ sử dụng                                      | Ghi chú quan trọng                                                                        |
| :------------ | :----------------------------------------------------- | :---------------------------------------------------------------------------------------- |
| **Backend**   | ASP.NET Core MVC                                       | Cookie Authentication, EF Core DbContext chung                                            |
| **Frontend**  | Razor Views + Tailwind (Tailwind CDN) + CSS tùy layout | Design theo palette xanh/Inter, spacing tối ưu UI                                         |
| **Database**  | SQL Server + Entity Framework Core                     | DB access qua `STEM.Web/Data/ApplicationDbContext.cs`                                     |
| **AI/Module** | AI tích hợp UI (inline trong Razor view)               | Hiện logic AI có thể là mock UI; khi tích hợp thật phải bảo đảm luồng “human-in-the-loop” |

# 📂 CẤU TRÚC THƯ MỤC CỐT LÕI (PROJECT STRUCTURE)

## Convention bắt buộc (bám theo CODING_GUIDE)

- Public site đặt ở root project (không dùng Area)
- Admin đặt ở `Areas/Admin`
- Teacher đặt ở `Areas/Teacher`
- Dùng `Models` + `ApplicationDbContext` tập trung cho EF entities
- Dùng `ViewComponent` cho block lặp lại hoặc query riêng (nếu cần)

## Kiểu cấu trúc hiện có trong repo

```text
/STEM
|-- STEM.Web/
|   |-- Program.cs
|   |-- appsettings.json
|   |-- Data/ApplicationDbContext.cs
|   |-- Models/
|   |-- Controllers/
|   |-- Views/
|   |-- Areas/
|   |   |-- Admin/
|   |   |   |-- Controllers/
|   |   |   |-- Models/ (admin-only view models)
|   |   |   |-- Views/
|   |   |   `-- ...
|   |   `-- Teacher/
|   |       |-- Controllers/
|   |       |-- Models/
|   |       `-- Views/
|   |-- Components/ (public)
|   `-- wwwroot/
```


# 📌 MAP ROUTING & AUTH (IMPORTANT)
## Routing
- `areas`: `{area:exists}/{controller=Dashboard}/{action=Index}/{id?}`
- `default`: `{controller=Home}/{action=Index}/{id?}`

## Auth
- Cookie Authentication
- `LoginPath = /Account/Login`
- Login tạo Claims:
  - `NameIdentifier = user.Id`
  - `GivenName = user.FullName`
  - `Role = user.Role.Name`
- Redirect theo role:
  - Admin → `Areas/Admin/Dashboard/Index`
  - Teacher → `Areas/Teacher/Dashboard/Index`
  - Student → `/StudentPortal/Index`

## Guard phổ biến
- Admin/Teacher controller có `[Authorize(Roles="Admin")]`, `[Authorize(Roles="Teacher")]`
- Student portal controller có `[Authorize(Roles="Student")]`


# 📜 QUY TẮC PHÁT TRIỂN (CODING STANDARDS)
## Trước khi code
1. Xác định đúng feature thuộc **Public / Student / Teacher / Admin**.
2. Nếu chưa hiểu logic nghiệp vụ hoặc DB field, phải đọc liên quan: entity/model + controller + view + layout.
3. Luôn đảm bảo feature không phá placement rules của CODING_GUIDE.

## Khi code
1. Không “đẻ” cấu trúc mới lạc hướng (không chuyển sang Clean Architecture hoàn toàn nếu repo chưa làm).
2. Backoffice phải nằm trong `Areas/Admin`.
3. Controller admin phải có `[Area("Admin")]`.
4. Entity/EF mapping chung để trong `/Models`, DbSet nằm trong `ApplicationDbContext`.
5. View model cho UI admin-only/teacher-only đặt trong đúng `Areas/<Role>/Models`.
6. Không duplicate entity giữa root `Models` và `Areas/Admin/Models`.
7. Tập trung query vào controller/action; dùng `AsNoTracking()` cho list read-only.
8. Upload/static path: dùng `wwwroot/uploads/...` và admin image storage là riêng (`Areas/Admin/Infrastructure/AdminImageStorage.cs`).

## Ngôn ngữ & UI

- Code/comment: ưu tiên Tiếng Anh hoặc Tiếng Việt nhất quán.
- UI: đồng nhất design system (Inter font, xanh primary, card/border radius 8px theo layout hiện có).
- Không thêm text “do AI viết” dạng mẫu; thay bằng text hướng dẫn UX.

## AI logic

- Luôn đảm bảo luồng:
  1. AI tạo gợi ý → 2) giáo viên xem → 3) có thể chỉnh → 4) lưu (human-in-the-loop).
- Nếu AI tích hợp thật:
  - validate input/format trước khi gọi AI
  - validate output trước khi lưu DB
  - tuyệt đối không lưu kết quả AI nếu giáo viên chưa “chấp nhận” (bằng cách paste/apply vào field rồi submit).

# 🔄 QUY TRÌNH NGHIỆP VỤ CHÍNH (KEY WORKFLOWS)

## Luồng Public

- Landing (`HomeController.Index`): banners đang active + 3 bài news published.
- Courses (`CoursesController.Index/Details`): danh sách khóa học có search/pagination; details theo `Code` đóng vai trò slug.

## Luồng Student

- `StudentPortalController.Index` (Authorize Student):
  - Lấy studentId từ claim
  - Load `StudentProfile` (1-1)
  - Lấy class student theo `Enrollments`
  - Upcoming sessions: Sessions của class trong tương lai
  - Recent feedbacks: Attendances đã present và session <= today (parse media urls từ JSON string hoặc chuỗi thô)
  - Invoices: Invoices + Payments, tính paid/due/status
  - Stats: sessions tuần này, feedback tuần gần nhất, media count, pending invoice count

## Luồng Teacher (core)

### Điểm danh

- `AttendancesController.Index`: filter theo today/open/upcoming/completed + search
- `AttendancesController.Board(GET)`:
  - Build board từ Sessions → Class → Enrollments
  - Nếu Attendance chưa tồn tại thì mặc định `IsPresent=true` và các field note/media null/empty
- `AttendancesController.Board(POST)`:
  - Validate session thuộc teacher
  - Upsert Attendance theo (SessionId, StudentId)
  - Normalize text (trim, null nếu rỗng)
  - Redirect về board

### Evidence

- `EvidenceController.Index/Details`:
  - Phân loại thiếu media / thiếu note / ready (cả note và media)
  - Tách media urls bằng split delimiter

### Equipment

- `EquipmentController.Index`: available = status==1 và không có borrow chưa trả
- `Borrow/Return`: tạo EquipmentBorrow + set Equipment.Status theo logic maintenance/active borrow

### Schedule

- `ScheduleController.Index`: filter + calendar tuần (Monday..Sunday)
- `Details`: load students + equipments borrow list + counts

## Luồng Admin

- `Admin/DashboardController.Index`: KPI metrics/alerts/revenue series
- `Admin/UsersController`: CRUD User + upsert StudentProfile nếu chọn role student

# 🎯 CHỈ DẪN CHO AI (AGENT INSTRUCTIONS)

Khi AI Agent được yêu cầu làm feature mới:

1. **Xác định domain & actor**: Public / Student / Teacher / Admin.
2. **Tìm điểm gốc dữ liệu**:
   - Entity trong `/Models` và DbSet trong `ApplicationDbContext`.
   - Controller hiện hữu liên quan để clone pattern.
3. **Tạo đúng placement**:
   - Public controller → `/Controllers`, view → `/Views/<Feature>/`.
   - Teacher controller → `/Areas/Teacher/Controllers`, view → `/Areas/Teacher/Views/<Feature>/`.
   - Admin controller → `/Areas/Admin/Controllers`, view → `/Areas/Admin/Views/<Feature>/`.
4. **Gắn layout phù hợp**:
   - Public: `_Layout.cshtml`
   - Auth: `_AuthLayout.cshtml`
   - Teacher: `_TeacherLayout.cshtml`
   - Student: `_StudentLayout.cshtml`
   - Admin: `_AdminLayout.cshtml`
5. **UI phải thân thiện & nhất quán**:
   - Title, filter/search có labels rõ ràng
   - Không tạo flow khiến user “không biết nhập gì”
6. **Kiểm tra lỗi nhỏ**:
   - Nếu gặp lỗi nhỏ (null ref, model binding, anti-forgery, route param), sửa ngay.
   - Nếu lỗi lớn về kiến trúc/scheme, báo người dùng.
7. **Revision/validate** sau thay đổi:
   - Đảm bảo view bind đúng model
   - Đảm bảo controller query đúng relationship schema
   - Đảm bảo role/area/Authorize không bị sai

# 📚 DANH SÁCH FILE “THƯỜNG DÙNG” ĐỂ NHANH CHÓNG HIỂU HỆ THỐNG

- `STEM.Web/Program.cs`
- `STEM.Web/Data/ApplicationDbContext.cs`
- `STEM.Web/Controllers/AccountController.cs`
- `STEM.Web/Controllers/HomeController.cs`
- `STEM.Web/Controllers/CoursesController.cs`
- `STEM.Web/Controllers/StudentPortalController.cs`
- `STEM.Web/Areas/Teacher/Controllers/*`
- `STEM.Web/Areas/Admin/Controllers/*`
- Layout shells:
  - `Views/Shared/_Layout.cshtml`
  - `Views/Shared/_AuthLayout.cshtml`
  - `Views/Shared/_StudentLayout.cshtml`
  - `Areas/Teacher/Views/Shared/_TeacherLayout.cshtml`
  - `Areas/Admin/Views/Shared/_AdminLayout.cshtml`

# ✅ CHECKLIST KHI HOÀN THÀNH MỘT FEATURE

- [ ] File đặt đúng area/public & đúng thư mục view
- [ ] Controller có đúng `[Area]` và `[Authorize]` cho actor
- [ ] Model bind & hidden inputs khớp với action POST
- [ ] Query đúng schema (FK, unique constraints) và không sai navigation
- [ ] UI consistent (spacing, button height ~48px, card/border radius)
- [ ] Có feedback cho người dùng (TempData toast/alert)
- [ ] Nếu dùng AI: luôn có bước apply/paste vào trường trước khi lưu

---

## AI Integration Strategy

- **Nguyên tắc vị trí:** AI không nằm ở sidebar như một menu rời. Thay vào đó, AI được "nhúng" (embed) trực tiếp vào các form tác vụ cụ thể của giáo viên (ví dụ: Sổ điểm danh, Chấm bài, Xem minh chứng).
- ** Loại AI: Gọi API từ Google AI studio cho 2 model AI Hỗ trợ nhận xét và phân tích minh chứng.**
- **Nguyên tắc chức năng:**
  - `Hỗ trợ nhận xét (Text Refinement):` Tích hợp cạnh ô ghi chú thô của giáo viên. Mục tiêu là giúp biên tập, làm mềm mỏng hóa câu từ trước khi gửi cho phụ huynh.
  - `Phân tích minh chứng (Video/Media Analysis):` Tích hợp tại khu vực nhập link media (ví dụ: Video thuyết trình dự án của học viên). AI sẽ tự động phân tích Điểm mạnh, Điểm yếu và đưa ra Đề xuất cải thiện.
- **Nguyên tắc giao diện:** Các nút bấm gọi AI nên được làm nổi bật tinh tế (ví dụ dùng icon ✨ và màu sắc phân biệt) nhưng không phá vỡ layout tổng thể. Kết quả AI trả về phải rõ ràng, dễ đọc (dùng markdown hoặc format đẹp) và luôn cho phép giáo viên chỉnh sửa lại (Human in the loop) trước khi lưu
