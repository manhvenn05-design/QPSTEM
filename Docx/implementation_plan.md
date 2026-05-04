# IMPLEMENTATION PLAN — QPSTEM STEM.Web

> **Stack:** ASP.NET Core MVC (.NET 10) · Vanilla CSS · EF Core · SQL Server  
> **Pattern:** aznews-style MVC — public root MVC + `Areas/Admin` backoffice  
> **Trạng thái tài liệu:** Cập nhật lần cuối 2026-04-30

---

## 📋 TỔNG KẾT HIỆN TRẠNG

### ✅ Đã hoàn thành
- Cấu trúc thư mục chuẩn (`Controllers`, `Models`, `Views`, `Areas/Admin`, `wwwroot/assets`, `wwwroot/admin`)
- `_Layout.cshtml` public: header, nav, footer với active-state logic
- `_AuthLayout.cshtml`: layout riêng cho trang đăng nhập / đăng ký
- **Trang chủ** (`Home/Index`): Hero, Features, Audience, Process, Consultation Form
- **Danh sách khóa học** (`Courses/Index`): 4 khóa học mock data
- **Chi tiết khóa học** (`Courses/Details`): hero, lợi ích, chương trình, giảng viên, related courses
- **Danh sách tin tức** (`News/Index`): article cards, sidebar featured, categories, tags
- **Chi tiết tin tức** (`News/Details`): hero, sections, key points, related articles
- **Trang đăng nhập** (`Account/Login`) + **Trang đăng ký** (`Account/Register`) — hiển thị form tĩnh
- `ViewModels` đầy đủ cho Courses và News
- `site.css` (36 KB) — design system, typography, component styles
- Cấu trúc `Areas/Admin` (thư mục đã tạo nhưng chưa có nội dung)
- `CODING_GUIDE.md` — quy chuẩn đầy đủ cho toàn dự án

### ⚠️ Chưa làm / Còn thiếu
- Không có Database / EF Core / DataContext
- Không có Authentication thực sự (ASP.NET Core Identity hoặc session)
- Admin Area: không có controller, view, layout nào
- Không có ViewComponent nào
- `Utilities/` trống hoàn toàn
- Data hiện tại 100% hardcode trong controller
- Không có `Areas/Admin/Views/_ViewStart.cshtml`
- Không có Area routing trong `Program.cs`
- `wwwroot/admin/assets` trống

---

## 🗺️ ROADMAP — CHECKLIST THEO PHASE

---

## PHASE 1 — NỀN TẢNG KỸ THUẬT (Foundation)

### 1.1 Program.cs & Routing
- [ ] Thêm Area routing vào `Program.cs` (đặt trước default route)
- [ ] Đăng ký EF Core + DbContext trong `Program.cs`
- [ ] Đăng ký ASP.NET Core Identity (hoặc Cookie Authentication tối giản)
- [ ] Cấu hình connection string trong `appsettings.json`

### 1.2 Database & EF Core
- [ ] Cài NuGet: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`
- [ ] Tạo `Models/DataContext.cs` với `DbContext` base
- [ ] Tạo entity: `Course` (Id, Title, Slug, Summary, ImageUrl, Category, Level, DurationText, PriceText, Description, IsActive)
- [ ] Tạo entity: `Article` (Id, Title, Slug, Excerpt, Content, ImageUrl, Category, AuthorName, PublishedAt, IsActive)
- [ ] Tạo entity: `User` / dùng `IdentityUser` nếu dùng Identity
- [ ] Tạo entity: `Student` (Id, FullName, DateOfBirth, ParentPhone, SchoolLevel, UserId)
- [ ] Tạo entity: `Class` (Id, Name, CourseId, TeacherId, Schedule, RoomName)
- [ ] Tạo entity: `Enrollment` (Id, StudentId, ClassId, EnrolledAt, Status)
- [ ] Tạo entity: `Instructor` (Id, FullName, Title, Bio, ImageUrl)
- [ ] Tạo entity: `Equipment` (Id, Name, Code, Quantity, Status, Description)
- [ ] Tạo entity: `Invoice` (Id, StudentId, Amount, PaidAt, Status, Description)
- [ ] Chạy `Add-Migration InitialCreate` + `Update-Database`
- [ ] Tạo `SeedData` hoặc SQL script để nạp dữ liệu mẫu ban đầu

### 1.3 Utilities
- [ ] Tạo `Utilities/Functions.cs` với các helper:
  - `SlugHelper.Slugify(string title)` — tạo slug từ tiêu đề tiếng Việt
  - `DateHelper.FormatVietnamese(DateTime date)` — format ngày kiểu "12 Th05, 2024"
  - `SessionHelper` — get/set auth session nếu không dùng Identity

---

## PHASE 2 — PUBLIC WEBSITE (Hoàn thiện)

### 2.1 Kết nối Controllers với Database
- [ ] `HomeController` — inject `DataContext`, lấy courses/articles nổi bật từ DB
- [ ] `CoursesController.Index` — query `DbSet<Course>`, bỏ hardcode
- [ ] `CoursesController.Details(slug)` — query theo slug + build `CourseDetailViewModel` từ DB
- [ ] `NewsController.Index` — query `DbSet<Article>`, pagination đơn giản (page, pageSize)
- [ ] `NewsController.Details(slug)` — query theo slug + build `NewsDetailViewModel`

### 2.2 ViewComponents (Public)
- [ ] Tạo `Components/FeaturedCoursesViewComponent.cs` + `Views/Shared/Components/FeaturedCourses/Default.cshtml`
  - Dùng ở trang chủ, hiển thị 3 khóa học nổi bật từ DB
- [ ] Tạo `Components/LatestArticlesViewComponent.cs` + `Views/Shared/Components/LatestArticles/Default.cshtml`
  - Dùng ở sidebar tin tức và trang chủ
- [ ] Tạo `Components/MenuViewComponent.cs` + `Views/Shared/Components/Menu/Default.cshtml` *(tuỳ chọn nếu menu cần dynamic)*

### 2.3 Trang còn thiếu (Public)
- [ ] **Trang Sự kiện / Events** (`Controllers/EventsController.cs` + `Views/Events/Index.cshtml`, `Details.cshtml`)
- [ ] **Student Portal** — trang học viên xem lịch học, bài tập (`Controllers/StudentPortalController.cs`)
- [ ] **Parent Portal** — trang phụ huynh xem tiến độ con (`Controllers/ParentPortalController.cs`)
- [ ] **Trang Giới thiệu / About** (`HomeController.About` + `Views/Home/About.cshtml`)
- [ ] **Trang Liên hệ / Contact** (`HomeController.Contact` + `Views/Home/Contact.cshtml`)

### 2.4 Form xử lý thực sự
- [ ] `AccountController.Login` [POST] — xác thực user + set cookie/session
- [ ] `AccountController.Register` [POST] — tạo tài khoản mới, hash password
- [ ] `AccountController.Logout` — clear session/cookie
- [ ] Consultation Form trên trang chủ — tạo model `LeadViewModel`, lưu vào DB hoặc gửi email

---

## PHASE 3 — ADMIN AREA (Backoffice)

### 3.1 Admin Infrastructure
- [ ] Tạo `Areas/Admin/Views/Shared/_LayoutAdmin.cshtml` — admin shell với sidebar
- [ ] Tạo `Areas/Admin/Views/_ViewStart.cshtml` — dùng `_LayoutAdmin`
- [ ] Tạo `Areas/Admin/Views/_ViewImports.cshtml` — import tag helpers
- [ ] Tải và đặt admin template CSS/JS vào `wwwroot/admin/assets/`
- [ ] Tạo `Areas/Admin/Controllers/HomeController.cs` — dashboard chính (`[Area("Admin")]`)

### 3.2 Admin Dashboard
- [ ] Tạo `Areas/Admin/Views/Home/Index.cshtml` — dashboard với stats cards:
  - Tổng học viên, Tổng khóa học, Doanh thu tháng, Lớp đang hoạt động
- [ ] Tạo `Areas/Admin/Components/AdminSidebarViewComponent` + `Default.cshtml`
  - Menu: Dashboard, Khóa học, Lớp học, Học viên, Thiết bị, Hóa đơn, Tin tức, Cài đặt

### 3.3 CRUD — Quản lý Khóa học
- [ ] `Areas/Admin/Controllers/CoursesController.cs` với `[Area("Admin")]`
  - Actions: `Index`, `Create` (GET/POST), `Edit` (GET/POST), `Delete` (GET/POST)
- [ ] `Areas/Admin/Views/Courses/Index.cshtml` — bảng danh sách + search + pagination
- [ ] `Areas/Admin/Views/Courses/Create.cshtml` — form tạo mới với rich text editor
- [ ] `Areas/Admin/Views/Courses/Edit.cshtml` — form chỉnh sửa
- [ ] `Areas/Admin/Views/Courses/Delete.cshtml` — confirm xóa

### 3.4 CRUD — Quản lý Lớp học (Classes)
- [ ] `Areas/Admin/Controllers/ClassesController.cs`
  - Actions: `Index`, `Create`, `Edit`, `Delete`, `Schedule`
- [ ] Views đầy đủ tương ứng
- [ ] Hiển thị danh sách học viên trong lớp (Enrollment)

### 3.5 CRUD — Quản lý Học viên (Students)
- [ ] `Areas/Admin/Controllers/StudentsController.cs`
  - Actions: `Index`, `Create`, `Edit`, `Details`, `Delete`
  - `Details` hiển thị: thông tin cá nhân, lịch sử đăng ký lớp, lịch sử thanh toán
- [ ] Views đầy đủ

### 3.6 CRUD — Quản lý Tin tức / Bài viết (CMS)
- [ ] `Areas/Admin/Controllers/ArticlesController.cs`
  - Actions: `Index`, `Create`, `Edit`, `Delete`
  - Tích hợp Summernote rich text editor
- [ ] `Areas/Admin/Controllers/FileController.cs` — upload ảnh
- [ ] Views đầy đủ

### 3.7 CRUD — Quản lý Thiết bị (Inventory)
- [ ] `Areas/Admin/Controllers/EquipmentController.cs`
  - Actions: `Index`, `Create`, `Edit`, `Delete`, `Borrow`, `Return`
- [ ] Views đầy đủ

### 3.8 CRUD — Quản lý Hóa đơn / Học phí (Billing)
- [ ] `Areas/Admin/Controllers/InvoicesController.cs`
  - Actions: `Index`, `Create`, `Edit`, `Details`
  - Filter theo tháng, trạng thái (Đã thu / Chưa thu)
- [ ] Views đầy đủ

### 3.9 Quản lý Người dùng (Users)
- [ ] `Areas/Admin/Controllers/UsersController.cs`
  - Actions: `Index`, `Create`, `Edit`, `Delete`, `ResetPassword`
  - Phân quyền: Admin, Teacher, Student, Parent
- [ ] Views đầy đủ

---

## PHASE 4 — AUTHENTICATION & AUTHORIZATION

- [ ] Chọn phương pháp auth: **ASP.NET Core Identity** (khuyến nghị) hoặc Session-based
- [ ] Cấu hình `[Authorize]` cho toàn bộ `Areas/Admin`
- [ ] Cấu hình `[Authorize(Roles = "Admin")]` cho các tác vụ nhạy cảm
- [ ] Redirect về `Areas/Admin/Login` nếu chưa đăng nhập
- [ ] Middleware kiểm tra role cho Student Portal và Parent Portal
- [ ] Tạo `Areas/Admin/Controllers/AuthController.cs`: Login, Logout

---

## PHASE 5 — FEATURES ĐẶC THÙ QPSTEM

### 5.1 AI Video Analysis (tính năng điểm nhấn)
- [ ] Tạo entity `VideoSession` (Id, StudentId, ClassId, VideoUrl, AnalysisResult, CreatedAt)
- [ ] Giao diện upload video bài thuyết trình cho học sinh
- [ ] Giao diện xem kết quả phân tích AI (điểm số, nhận xét)
- [ ] Tích hợp API AI (Google Gemini / OpenAI) để phân tích video
- [ ] Admin/Teacher xem danh sách video và kết quả phân tích

### 5.2 Lịch học (Schedule)
- [ ] Trang lịch học cho học sinh (`StudentPortal/Schedule`)
- [ ] Trang lịch dạy cho giáo viên
- [ ] Admin quản lý lịch (`Areas/Admin/Controllers/ScheduleController`)
- [ ] Hiển thị calendar view (FullCalendar.js)

### 5.3 Điểm danh (Attendance)
- [ ] Entity `Attendance` (Id, ClassId, StudentId, Date, Status)
- [ ] Admin/Teacher: điểm danh theo buổi học
- [ ] Student/Parent: xem lịch sử điểm danh

### 5.4 Thông báo (Notifications)
- [ ] Entity `Notification` (Id, UserId, Title, Message, IsRead, CreatedAt)
- [ ] `Components/NotificationViewComponent` — hiển thị badge và dropdown
- [ ] Admin: gửi thông báo tới học sinh/phụ huynh theo lớp

---

## PHASE 6 — POLISH & PRODUCTION

### 6.1 UX / Performance
- [ ] Pagination cho tất cả trang Index (admin và public)
- [ ] Search + Filter cho admin tables
- [ ] Responsive CSS cho mobile (kiểm tra tất cả breakpoints)
- [ ] Loading state và empty state cho các danh sách
- [ ] SEO: meta description, og:image cho từng trang public
- [ ] Schema markup JSON-LD cho trang khóa học

### 6.2 Error Handling & Validation
- [ ] Server-side validation với `[Required]`, `[MaxLength]`, custom validator
- [ ] Client-side validation với jQuery Unobtrusive Validation
- [ ] Custom error pages: `Views/Home/Error.cshtml` (404, 500)
- [ ] Global exception handler trong `Program.cs`

### 6.3 Security
- [ ] Anti-forgery token cho tất cả form POST
- [ ] HTTPS redirect
- [ ] Input sanitization cho rich text content
- [ ] Rate limiting cho login form

### 6.4 Testing & Deployment
- [ ] Kiểm thử thủ công tất cả flow chính (đăng ký, đăng nhập, admin CRUD)
- [ ] Build release và kiểm tra không có lỗi compile
- [ ] Migration script cho production DB
- [ ] Cấu hình hosting (IIS / Azure App Service / VPS)
- [ ] Cấu hình CI/CD cơ bản (tuỳ chọn)

---

## 🎯 THỨ TỰ ƯU TIÊN GỢI Ý

| Thứ tự | Phase | Mô tả |
|--------|-------|-------|
| 1 | Phase 1 | Database + EF Core + Program.cs routing |
| 2 | Phase 4 | Authentication (Login/Logout thực sự) |
| 3 | Phase 3.1–3.2 | Admin layout + Dashboard |
| 4 | Phase 3.3–3.5 | Admin CRUD: Courses, Students, Classes |
| 5 | Phase 2.1–2.2 | Kết nối public site với DB + ViewComponents |
| 6 | Phase 3.6–3.9 | Còn lại của Admin (CMS, Billing, Inventory) |
| 7 | Phase 5 | Features đặc thù QPSTEM (AI Video, Schedule) |
| 8 | Phase 6 | Polish, Security, Deployment |
