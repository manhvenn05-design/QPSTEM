# CODING GUIDE FOR STEM MVC PROJECT
# Trước khi coding
Luôn đảm bảo đã hiểu project mới code còn không hiểu đoạn nào thì báo cho tôi để tôi cung cấp thông tin.
# trong lúc coding
Nếu gặp các lỗi nhỏ trong lúc làm 1 chức năng gì đó mà bạn có khả năng sửa luôn thì hãy sửa luôn, còn lỗi đó quá lớn hãy nhắc tôi.
# sau khi coding
Luôn revision để xem mình đã làm đúng chưa, project đang đi đúng hướng không, có sai sót nữa không. chứ không được coding xong là bỏ đó không biết là đúng hay sai.
# Khi nhận yêu cầu tạo giao diện
Luôn tạo giao diện đồng nhất với hệ thống từ màu sắc, bố cục,font và đặc biệt là khớp với DB để không lệch schema.
# Yêu cầu phải đạt được
Giao diện và cả logic nghiệp vụ phải thân thiện với người dùng, có hướng dẫn rõ ràng không để gặp tình trang người dùng khó sử dụng tính năng nào hay nhập liệu thông tin gì. Mọi thao tác nhanh gọn KHÔNG ĐƯỢC rườm rà phức tạp tốn nhiều thời gian.
# khi tạo ra code
Không tạo ra các câu ví dụ như " Admin không nên thao tác trên từng record rời rạc. Màn này gom điểm danh theo từng buổi để kiểm soát tình trạng lớp, phát hiện buổi chưa chốt và mở bảng học viên nhanh hơn" những câu này làm người dùng biết đây là do AI tạo ra. Thay vì những đoạn text như vậy hay thay bằng những text giới thiệu hay hướng dẫn.
# Trong quá trình làm Project
Luôn đưa ra những ý tưởng đọc đáo (không nên quá cứng nhắc theo luồng mà tôi đã định sẵn) để tôi có thể thêm vào nhằm tạo ấn tượng, luôn ra soát và nắm rõ hệ thống, để luôn luôn bám sát được nhằm nâng cao tính đồng nhất khi bạn tạo code.
Luôn cải tiến không ngừng để tạo ra 1 hệ thống đọc đáo tối ưu nhất.
Không dừng lại ở dùng được mà phải tối ưu.
## Mục tiêu

Tài liệu này là `rulebook thực thi` để Codex và người triển khai code đúng cấu trúc thư mục mong muốn cho dự án STEM.

Guide này bám theo `pattern aznews tutorial structure` đã phân tích từ bộ PDF:

- Public website dùng root ASP.NET Core MVC project
- Dùng `Models` + `DataContext` tập trung
- Dùng `ViewComponent` cho các block giao diện lặp lại
- Dùng `Areas/Admin` để tách backoffice
- Dùng `wwwroot` cho static assets, trong đó admin assets nằm riêng dưới `wwwroot/admin`

Guide này không mô tả lại bài học từng bước. Nó chỉ quy định cách tổ chức mã nguồn và cách thêm feature mới cho đúng style.

## Core Structure Convention

Project phải ưu tiên cấu trúc sau:

```text
/<project-root>
|-- Areas/
|   `-- Admin/
|       |-- Controllers/
|       |-- Models/
|       |-- Components/
|       `-- Views/
|           |-- Home/
|           `-- Shared/
|               |-- Components/
|               `-- _LayoutAdmin.cshtml
|-- Components/
|-- Controllers/
|-- Models/
|-- Utilities/
|-- Views/
|   |-- Home/
|   `-- Shared/
|       |-- Components/
|       `-- _Layout.cshtml
|-- wwwroot/
|   |-- assets/
|   `-- admin/
|       `-- assets/
|-- Program.cs
`-- appsettings.json
```

Quy ước bắt buộc:

- Public site đặt ở root project, không đặt trong `Area`
- Backoffice đặt trong `Areas/Admin`
- `Models/DataContext.cs` là điểm kết nối EF Core chung
- `Components` ở root chỉ dùng cho public site
- `Areas/Admin/Components` chỉ dùng cho admin
- View của `ViewComponent` luôn nằm tại `Views/Shared/Components/<ComponentName>/Default.cshtml` hoặc `Areas/Admin/Views/Shared/Components/<ComponentName>/Default.cshtml`
- `Utilities/Functions.cs` dùng cho helper dùng lại ở nhiều nơi; không nhét business flow dài vào đây

## Placement Rules

### Controller placement

- Public page controller đặt trong `/Controllers`
- Admin controller đặt trong `/Areas/Admin/Controllers`
- Mọi admin controller phải có `[Area("Admin")]`
- Không trộn action public và action admin trong cùng một controller

Ví dụ:

- `CoursesController` cho public portal: `/Controllers/CoursesController.cs`
- `CoursesController` cho admin management: `/Areas/Admin/Controllers/CoursesController.cs`

Nếu trùng tên giữa public và admin thì namespace/area phải là cơ chế tách biệt, không đổi tên gượng ép sang kiểu `AdminCoursesController` trừ khi project hiện hữu đã theo style đó.

### Model placement

- Entity và model dùng chung toàn hệ thống ưu tiên đặt trong `/Models`
- `DataContext.cs` đặt trong `/Models`
- Model chỉ phục vụ admin UI, editor config, auth form, upload config có thể đặt trong `/Areas/Admin/Models`
- Không tạo duplicate entity giữa root `Models` và `Areas/Admin/Models`

Ưu tiên:

- Bảng DB và EF entity: `/Models`
- Admin-only view helper model: `/Areas/Admin/Models`

### View placement

- View public đặt trong `/Views/<ControllerName>/`
- Layout public đặt trong `/Views/Shared/_Layout.cshtml`
- View admin đặt trong `/Areas/Admin/Views/<ControllerName>/`
- Layout admin đặt trong `/Areas/Admin/Views/Shared/_LayoutAdmin.cshtml`
- `_ViewStart.cshtml` và `_ViewImports.cshtml` của admin phải nằm trong `Areas/Admin/Views` nếu admin có layout riêng hoặc tag helper/import riêng

### Partial và ViewComponent placement

- Dùng `ViewComponent` cho block có dữ liệu riêng, truy vấn riêng, hoặc tái sử dụng nhiều nơi
- Chỉ dùng partial view nếu block chỉ là render UI đơn giản và không có query/data assembly riêng
- Class `ViewComponent` đặt trong `Components` hoặc `Areas/Admin/Components`
- Tên class theo mẫu `<Feature>ViewComponent`
- Tên thư mục view component theo tên component bỏ hậu tố `ViewComponent`

Ví dụ:

- Class: `MenuViewComponent`
- View: `/Views/Shared/Components/Menu/Default.cshtml`

### Utility placement

- Helper tĩnh dùng chung đặt trong `/Utilities/Functions.cs` hoặc file helper tương đương trong `Utilities`
- Chỉ đặt ở `Utilities` những logic như slug helper, format helper, session/auth helper, upload path helper
- Không đặt query DB hoặc logic nghiệp vụ dài trong `Utilities`

## Naming Rules

- Class dùng `PascalCase`
- Controller dùng `<Feature>Controller`
- ViewComponent dùng `<Feature>ViewComponent`
- View mặc định trùng tên action: `Index.cshtml`, `Details.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Delete.cshtml`
- Component view luôn là `Default.cshtml`
- Tên thư mục controller/view/module dùng English domain terms
- Tên method/action ngắn, đúng ý nghĩa nghiệp vụ: `Index`, `Details`, `Create`, `Edit`, `Delete`, `Schedule`, `Attendance`, `Invoice`, `Borrow`, `Return`

Không dùng:

- Tên viết tắt khó đoán
- Trộn tiếng Việt vào tên class/file
- Một feature nhiều kiểu tên khác nhau giữa controller, view folder, component folder

## Mapping từ domain STEM sang folder

### Public modules

Các tính năng hướng phụ huynh, học sinh, khách truy cập đặt ở root MVC:

- `Home`
- `Courses`
- `Classes`
- `Sessions`
- `Posts`
- `Events`
- `StudentPortal`
- `ParentPortal`
- `Billing` nếu là phần tra cứu hóa đơn cho phụ huynh

Vị trí:

- Controller: `/Controllers`
- Views: `/Views/<Feature>`
- Component dùng lại: `/Components` và `/Views/Shared/Components`

### Admin modules

Các tính năng vận hành và quản trị đặt trong `Areas/Admin`:

- `Users`
- `Academic`
- `Inventory`
- `Billing`
- `CRM`
- `CMS`
- `Reports`
- `Settings`

Vị trí:

- Controller: `/Areas/Admin/Controllers`
- View: `/Areas/Admin/Views/<Feature>`
- Admin component: `/Areas/Admin/Components`
- Admin-only UI helper models: `/Areas/Admin/Models`

### Mapping pattern từ aznews sang STEM

Ánh xạ triển khai mặc định:

- `tblMenu` pattern -> navigation/menu structure cho website hoặc admin sidebar
- `tblPost` pattern -> `Posts`, `News`, `Events`, `Announcements`
- `Details` page pattern -> chi tiết `Post`, `Course`, `Session summary`, `Event`
- `AdminMenu` pattern -> menu sidebar trong `Areas/Admin`
- `Post CRUD` pattern -> CRUD cho `Posts`, `Banners`, `Courses`, `Equipments`, `Invoices`
- `Register/Login/Logout` pattern -> admin authentication flow hoặc staff portal auth flow

## Feature Implementation Patterns

### 1. Thêm public page

Khi thêm một trang public mới:

1. Tạo controller trong `/Controllers`
2. Tạo action tương ứng
3. Nếu cần dữ liệu DB, dùng entity trong `/Models` và truy cập qua `DataContext`
4. Tạo view trong `/Views/<ControllerName>/`
5. Dùng layout public `_Layout.cshtml`
6. Nếu lấy block từ template HTML, chỉ chuyển phần nội dung riêng của page vào view; phần khung chung phải nằm ở layout hoặc component

Áp dụng cho:

- trang giới thiệu khóa học
- trang lịch học
- trang thư viện sản phẩm học sinh
- trang bài viết/tin tức

### 2. Thêm admin page

Khi thêm trang admin:

1. Tạo controller trong `/Areas/Admin/Controllers`
2. Gắn `[Area("Admin")]`
3. Tạo view trong `/Areas/Admin/Views/<ControllerName>/`
4. Dùng `_LayoutAdmin.cshtml`
5. Chỉ dùng assets từ `~/admin/assets/` cho template admin

Áp dụng cho:

- dashboard
- quản lý học viên
- quản lý lớp
- quản lý thiết bị
- quản lý hóa đơn

### 3. Thêm CRUD screen trong `Areas/Admin`

Mặc định CRUD admin theo pattern bài `Menu` và `Post`:

- Controller có các action:
  - `Index`
  - `Create` GET/POST
  - `Edit` GET/POST
  - `Delete` GET/POST
  - `Details` nếu nghiệp vụ cần xem chi tiết
- Views tương ứng:
  - `Index.cshtml`
  - `Create.cshtml`
  - `Edit.cshtml`
  - `Delete.cshtml`
  - `Details.cshtml` nếu có

Quy tắc:

- Dùng entity chung từ `/Models` nếu dữ liệu map trực tiếp với DB
- Dùng admin view model riêng nếu form phức tạp hoặc có upload/editor/status mapping
- Không nhét HTML lặp lại của bảng, form block, sidebar trực tiếp vào nhiều file nếu có thể component hóa
- Pagination, filter, search là concern của `Index` admin, không đẩy vào public detail logic

### 4. Thêm `ViewComponent`

Dùng `ViewComponent` khi block có một trong các dấu hiệu sau:

- xuất hiện ở nhiều trang
- có truy vấn DB riêng
- có logic chọn dữ liệu riêng
- là một khu vực template lớn không nên để trực tiếp trong layout/page

Ưu tiên dùng cho:

- menu điều hướng public
- admin sidebar menu
- banner section
- danh sách bài viết nổi bật
- widget lịch học gần nhất
- widget thông báo

Mẫu triển khai:

1. Tạo class `<Feature>ViewComponent`
2. Inject `DataContext` hoặc service cần thiết
3. Trả view với model rõ ràng
4. Tạo `Default.cshtml` trong đúng thư mục component view
5. Gọi từ layout/view qua `@await Component.InvokeAsync("Feature")`

Không dùng `ViewComponent` chỉ để bọc một mẩu HTML tĩnh không có lý do tái sử dụng thật.

### 5. Thêm detail page

Detail page theo pattern `TH05`:

- Action `Details(...)` nằm ở controller public hoặc admin tùy audience
- View `Details.cshtml` đặt theo controller tương ứng
- Nếu URL cần đẹp, slug helper có thể đặt ở `Utilities/Functions.cs`
- Chỉ phần nội dung chính của detail page nằm trong `Details.cshtml`; phần khung/chung nên tiếp tục dùng layout và component

Áp dụng cho:

- chi tiết bài viết
- chi tiết khóa học
- chi tiết sự kiện
- chi tiết buổi học đã diễn ra

### 6. Thêm rich text editor hoặc file picker

Pattern từ `Summernote + elFinder` chỉ áp dụng cho các màn admin có nhu cầu biên tập nội dung hoặc quản lý file:

- `Posts`
- `CMS`
- `Lesson materials`
- `Banners`
- mô tả `Courses`

Quy tắc:

- Editor và file manager chỉ đặt trong `Areas/Admin`
- Tài nguyên JS/CSS của editor/file manager đặt dưới `wwwroot/admin`
- Controller quản lý file đặt trong `/Areas/Admin/Controllers`
- Model cấu hình editor có thể đặt trong `/Areas/Admin/Models`
- Không kéo dependency editor vào public site nếu public site chỉ render HTML đã lưu

## View Composition Rules

Khi convert template HTML sang MVC:

- `_Layout.cshtml` hoặc `_LayoutAdmin.cshtml` chứa shell chung
- `@RenderBody()` là điểm cắm phần nội dung riêng của trang
- `header`, `footer`, `sidebar`, `topbar`, `menu`, `post block`, `banner block` nên được cân nhắc tách ra component nếu có reuse hoặc query riêng
- Không copy nguyên file HTML template vào nhiều view rồi sửa tay từng chỗ

Thứ tự ưu tiên:

1. Layout cho shell chung
2. ViewComponent cho block dùng lại có data riêng
3. Partial cho UI fragment đơn giản
4. Page view cho nội dung đặc thù của action

## Data Access Rules

- Dùng `DataContext` làm entry point chuẩn cho EF Core
- `DbSet<>` cho entity chung phải khai báo trong `/Models/DataContext.cs`
- Không tạo nhiều `DbContext` nếu chưa có lý do rõ ràng
- Query hiển thị danh sách, chi tiết, menu, sidebar nên bám pattern đơn giản từ tutorial trước khi nghĩ đến abstraction mới
- Nếu sau này project lớn hơn, service/repository có thể bổ sung, nhưng vẫn phải giữ placement rules ở trên cho MVC/UI layer

## Auth and Admin Guard Rules

Theo pattern tutorial:

- Admin auth flow thuộc `Areas/Admin`
- `Login`, `Register`, `Logout` của admin nằm trong `/Areas/Admin/Controllers`
- View auth nằm trong `/Areas/Admin/Views/Login` và `/Areas/Admin/Views/Register`
- Logic helper cho session/auth state nếu đơn giản có thể đặt trong `Utilities/Functions.cs`

Tuy nhiên, với dự án STEM thật:

- Chỉ dùng kiểu auth helper đơn giản này khi project đang bám pattern tutorial
- Nếu sau này chuyển sang `ASP.NET Core Identity`, vẫn giữ nguyên nguyên tắc tách admin/public và không trộn auth pages lung tung ngoài `Area`

## STEM-Specific Default Decisions

Khi Codex code cho dự án STEM, mặc định áp dụng các quyết định sau:

- Phụ huynh/học sinh là public-facing area, không đặt trong `Areas/Admin`
- Nội dung quản trị vận hành luôn đặt trong `Areas/Admin`
- `Courses`, `Classes`, `Sessions`, `Posts`, `Banners`, `Equipments`, `Invoices` ưu tiên CRUD theo admin pattern
- `Posts`, `Banners`, `Announcements`, `Events` có thể dùng pattern từ `tblPost`
- `Menu`, `Admin sidebar`, `Home sections`, `Latest posts`, `Upcoming sessions`, `Notifications` ưu tiên `ViewComponent`
- `Teaching materials`, `CMS content`, `course description` là nơi hợp lý để dùng rich text/editor integration

## Codex Execution Checklist

Trước khi thêm code mới, phải tự kiểm theo checklist này:

- Feature này thuộc public hay admin?
- Nếu là admin, đã đặt trong `Areas/Admin` chưa?
- Controller đã ở đúng thư mục chưa?
- View đã ở đúng thư mục theo controller/action chưa?
- Đang dùng đúng layout chưa?
- Block này nên là page HTML, partial hay `ViewComponent`?
- Có đang copy HTML lặp lại thay vì tách component không?
- Model này là entity dùng chung hay admin-only helper model?
- Có cần thêm `DbSet<>` vào `DataContext` không?
- Static assets có đang đặt đúng `wwwroot/assets` hoặc `wwwroot/admin/assets` không?
- Có đang giữ naming nhất quán giữa controller, view folder và component folder không?
- Có đang bám pattern `aznews` nhưng map đúng domain STEM không?

## Không làm

Các lượt code sau không nên làm các việc sau nếu không có chỉ định rõ:

- Không tự ý đổi cấu trúc project sang Razor Pages, Clean Architecture, hay feature folders hoàn toàn khác
- Không trộn admin pages vào root `Views`
- Không đặt public pages vào `Areas/Admin`
- Không duplicate layout, menu HTML, hoặc sidebar HTML ở nhiều file
- Không tạo helper/service vô tội vạ chỉ để bọc một đoạn logic rất nhỏ
- Không đặt editor/file manager assets ở public root nếu chúng chỉ dùng cho admin

## Kết luận vận hành

Khi triển khai feature mới cho dự án STEM, Codex phải hiểu guide này theo nguyên tắc:

- Bám `ASP.NET Core MVC` kiểu tutorial `aznews`
- Tách rõ `public MVC` và `Areas/Admin`
- Dùng `DataContext` tập trung
- Dùng `ViewComponent` cho block lặp lại hoặc có query riêng
- Dùng naming và placement nhất quán
- Ưu tiên code đúng cấu trúc thư mục trước rồi mới tối ưu thêm

Nếu một yêu cầu mới không ghi rõ vị trí đặt file, Codex phải mặc định suy ra từ guide này thay vì tự phát minh cấu trúc mới.
