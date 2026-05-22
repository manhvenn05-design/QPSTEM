# STEM System Report Support Guide

## 1. Muc dich cua tai lieu nay

Tai lieu nay duoc viet de ho tro viec bao cao he thong voi giang vien trong boi canh du an duoc phat trien theo phong cach "vibe coding". Muc tieu cua file nay khong chi de mo ta he thong dang co, ma con de giup:

- Hieu he thong theo goc nhin kien truc, nghiep vu va van hanh.
- Biet duoc moi module dung de giai bai toan gi.
- Biet duoc du lieu di nhu the nao tu giao dien den database.
- Biet cach tra loi khi giang vien hoi "tai sao em lam nhu vay?"
- Biet truoc cac diem yeu, no ky thuat va han che hien tai de phan bien co co so.

Noi dung duoi day bam sat codebase hien tai trong repo `STEM.Web`.

---

## 2. Tong quan he thong

### 2.1. Bai toan he thong giai quyet

He thong STEM nay la mot web app quan ly trung tam/lop hoc STEM, tap trung vao 3 nhom doi tuong:

- Admin:
  - Quan ly nguoi dung
  - Quan ly khoa hoc, lop hoc, lich hoc
  - Quan ly diem danh, minh chung, tai chinh, luong
  - Quan ly thiet bi, leads, CMS
- Teacher:
  - Xem lich day
  - Diem danh hoc sinh
  - Nhap ghi chu cho tung hoc sinh
  - Upload media minh chung
  - Muon/tra thiet bi
  - Theo doi luong tam tinh
- Student/Parent:
  - Xem lich hoc
  - Xem nhan xet va minh chung
  - Xem hoc phi
  - Xem thong tin ca nhan

### 2.2. Gia tri co loi cua he thong

He thong khong chi la CRUD quan ly lop hoc, ma co 3 gia tri noi bat:

1. So hoa tien trinh hoc tap:
   - Moi buoi hoc co the gan diem danh, nhan xet, media, AI evaluation.

2. Lien thong nghiep vu:
   - Lich hoc -> diem danh -> minh chung -> payroll -> tai chinh.

3. Ho tro van hanh trung tam:
   - Khong chi phuc vu hoc sinh/phu huynh, ma con phuc vu admin va giao vien nhu mot he thong ERP mini cho trung tam STEM.

---

## 3. Nen tang ky thuat

### 3.1. Cong nghe chinh

- Backend framework: ASP.NET Core MVC
- ORM: Entity Framework Core + SQL Server
- Authentication: Cookie Authentication
- File storage: Local file storage (`wwwroot/uploads/...`)
- AI integration: Gemini service qua `IAIService` / `GeminiAiService`
- UI: Razor Views + server-rendered MVC

### 3.2. Khoi tao he thong

Trong [Program.cs](</d:/STEM/STEM.Web/Program.cs:1>), he thong:

- Doc connection string `DefaultConnection`
- Cau hinh `ApplicationDbContext`
- Cau hinh Cookie Auth
- Dang ky `ControllersWithViews`
- Dang ky cache, antiforgery, http client
- Dang ky business services:
  - `AttendanceWorkflowService`
  - `PayrollCalculationService`
  - `IAdminShellService`
  - `ITeacherShellService`
  - `IFileStorageService -> LocalFileStorageService`
- Map routes cho area va default route

### 3.3. Kieu kien truc

He thong dang o dang:

- MVC truyen thong
- View server-rendered bang Razor
- DbContext trung tam
- Business logic nam o:
  - Controller
  - Service
  - Mot phan rule helper

Day khong phai kien truc Domain-Driven Design day du, nhung la mot kien truc thuc dung, phu hop voi do an/ung dung van hanh vua va nho.

---

## 4. Cau truc module

### 4.1. Areas

He thong tach theo `Areas`:

- `Admin`
- `Teacher`
- `Student`

Muc dich:

- Tach giao dien va nghiep vu theo vai tro
- De phan quyen
- De giam xung dot route/controller

### 4.2. Controller chinh theo vai tro

#### Admin

Controllers trong `STEM.Web/Areas/Admin/Controllers`:

- `DashboardController`
- `UsersController`
- `CoursesController`
- `ClassesController`
- `SessionsController`
- `AttendancesController`
- `FinanceController`
- `PayrollController`
- `PayRateConfigController`
- `InventoryController`
- `LeadsController`
- `CmsController`
- `SettingsController`

#### Teacher

Controllers trong `STEM.Web/Areas/Teacher/Controllers`:

- `DashboardController`
- `ScheduleController`
- `AttendancesController`
- `EvidenceController`
- `AICopilotController`
- `EquipmentController`
- `PayrollController`

#### Student

Controller chinh:

- `StudentPortalController`

### 4.3. Dich vu nghiep vu quan trong

#### `AttendanceWorkflowService`

Vai tro:

- Validate media URL
- Kiem tra khoa chinh sua diem danh
- Recompute payroll status cho tung session

Y nghia:

- Day la service noi giua diem danh va tinh luong.

#### `PayrollCalculationService`

Vai tro:

- Sinh bang luong thang
- Tinh uoc tinh luong real-time
- Ap dung bonus/penalty

Y nghia:

- Day la business core cua phan luong giao vien.

#### `IFileStorageService` / `LocalFileStorageService`

Vai tro:

- Luu file upload vao local storage
- Xoa file
- Tra URL download

Y nghia:

- Toan bo avatar, media, tai lieu hien tai deu dua tren storage local.

---

## 5. Mo hinh du lieu

### 5.1. DbContext trung tam

Trong [ApplicationDbContext.cs](</d:/STEM/STEM.Web/Data/ApplicationDbContext.cs:1>) co cac `DbSet` quan trong:

- `Users`
- `Roles`
- `StudentProfiles`
- `TeacherProfiles`
- `Courses`
- `Classes`
- `Sessions`
- `Enrollments`
- `Attendances`
- `AttendanceSkillScores`
- `Invoices`
- `Payments`
- `PayrollRecords`
- `PayRateConfigs`
- `Equipments`
- `EquipmentBorrows`
- `Rooms`
- `Posts`
- `Leads`
- `Banners`

### 5.2. Quan he nghiep vu cot loi

#### User va role

- `User` gan `Role`
- `User` co the co `StudentProfile` hoac `TeacherProfile`

#### Course -> Class -> Session

- `Course` la khoa hoc
- `Class` la lop hoc cu the mo cho khoa hoc
- `Session` la tung buoi hoc cua lop

#### Student -> Enrollment -> Class

- Hoc sinh khong vao thang `Class`
- Hoc sinh vao lop qua `Enrollment`

#### Session -> Attendance

- Moi `Attendance` la 1 hoc sinh trong 1 buoi hoc
- Chua:
  - `IsPresent`
  - `IsExcused`
  - `TeacherRawNote`
  - `ProductMediaUrls`
  - `AiEvaluation`

#### Tai chinh

- `Invoice` gan voi hoc sinh, co the lien quan den lop
- `Payment` gan voi invoice

#### Luong

- `Session` co `PayrollStatus`, `SessionRateApplied`
- `PayrollRecord` la ket qua tong hop theo thang/giao vien
- `PayRateConfig` la bang gia theo tier va do kho khoa hoc

### 5.3. Mot so quyet dinh thiet ke quan trong

1. Media minh chung ca nhan nam trong `Attendance.ProductMediaUrls`
2. Media chung cua buoi/lop nam trong `Session.ClassMediaUrls`
3. Teacher thay duoc session theo:
   - giao vien chinh khi `SubstituteTeacherId == null`
   - giao vien day thay khi `SubstituteTeacherId == teacherId`

Day la diem rat quan trong vi lien quan truc tiep den thong ke dashboard, payroll va quyen thao tac.

---

## 6. Luong nghiep vu chinh

## 6.1. Dang nhap va phan quyen

Trong [AccountController.cs](</d:/STEM/STEM.Web/Controllers/AccountController.cs:1>):

- User dang nhap bang username hoac email
- Mat khau hash bang BCrypt
- Role claim duoc canonicalize qua `AppRoles`
- Sau khi login:
  - Admin -> area Admin
  - Teacher -> area Teacher
  - Student -> area Student

### Cac diem co the bi hoi

Hoi: "Tai sao dung Cookie Auth thay vi JWT?"

Tra loi goi y:

- Day la he thong MVC server-rendered, khong phai SPA/API-first.
- Cookie auth phu hop hon vi:
  - Tich hop tu nhien voi Razor/MVC
  - Don gian khi quan ly phien dang nhap
  - Giam do phuc tap so voi access token/refresh token

---

## 6.2. Quan ly hoc sinh, giao vien, role

Admin thao tac qua `UsersController`.

Chuc nang:

- Tao user
- Sua user
- Upload avatar
- Gan role
- Tao/xoa `StudentProfile`, `TeacherProfile` theo role
- Khoa/mo khoa tai khoan
- Xoa mem, xoa manh tay (`Purge`) du lieu test

### Diem ky thuat dang chu y

- `UsersController` dang xu ly kha nhieu nghiep vu trong mot controller.
- Da co phan validate role, profile, avatar.
- Da sua mot loi quan trong:
  - tranh `NullReference` khi user co role hoc sinh nhung field giam ho null.

### Cau hoi giang vien co the hoi

Hoi: "Tai sao em cho role quyet dinh profile?"

Tra loi goi y:

- Vi nghiep vu cua he thong phan tach ro hoc sinh va giao vien.
- Cung mot bang `Users` giup thong nhat auth.
- Profile tach rieng giup tranh cot du thua tren bang user.

---

## 6.3. Quan ly khoa hoc, lop hoc, lich hoc

Admin quan ly qua:

- `CoursesController`
- `ClassesController`
- `SessionsController`

### Quy trinh

1. Tao course
2. Tao class gan course + giao vien
3. Sinh session theo lich
4. Dieu chinh session thu cong khi can

### Y nghia business

Day la xương song van hanh cua he thong. Toan bo diem danh, minh chung, payroll va hoc phi deu phat sinh tu `Session`.

### Diem hay de bao cao

- He thong co 2 muc:
  - Muc hoc thuat: Course
  - Muc van hanh: Class / Session

No tot hon kieu thiet ke chi co "khoa hoc" va "lich hoc" vi:

- de quan ly nhieu dot mo lop
- de tai su dung khoa hoc
- de tinh luong, diem danh, hoc phi theo lop hoc thuc te

---

## 6.4. Diem danh va nhan xet hoc sinh

Teacher thao tac qua `AttendancesController` va view board.

Trong [Board.cshtml](</d:/STEM/STEM.Web/Areas/Teacher/Views/Attendances/Board.cshtml:161>), giao vien co:

- Trang thai diem danh
- `TeacherRawNote` cho tung hoc sinh
- upload video minh chung cho tung hoc sinh
- goi AI viet lai ghi chu

### Du lieu tao ra

Moi hoc sinh trong moi buoi co:

- `TeacherRawNote`
- `ProductMediaUrls`
- `AiEvaluation` (neu co xu ly AI)

### Nghiep vu thuc te

He thong cua ban khong luu "nhan xet ca buoi hoc".
No luu "nhan xet theo tung hoc sinh".

Day la diem rat quan trong khi bao cao, vi giang vien co the hoi:

Hoi: "Tai sao phan Student lai hien nhan xet?"

Tra loi goi y:

- Nhan xet o phia Student la nhan xet ca nhan cua hoc sinh do trong session.
- Khong phai nhan xet chung cua buong hoc.

---

## 6.5. Minh chung hoc tap

He thong co 2 loai minh chung:

1. Minh chung ca nhan:
   - `Attendance.ProductMediaUrls`
2. Minh chung chung cua buoi/lop:
   - `Session.ClassMediaUrls`

### Luong hien tai

- Giao vien co the upload media chung trong `Schedule/Details`
- File duoc luu vao:
  - `wwwroot/uploads/session-media`
- Hoc sinh xem minh chung trong `StudentPortal/Evidence`

### Diem da duoc sua

Portal hoc sinh da duoc sua de:

- doc ca `ProductMediaUrls`
- doc them `Session.ClassMediaUrls`
- gop 2 nguon minh chung lai
- phan loai thanh:
  - video
  - anh
  - external links

### Diem can luu y khi bao cao

Neu giang vien hoi:

Hoi: "Tai sao 1 hoc sinh lai thay media cua ca lop?"

Tra loi goi y:

- Vi he thong co nghiep vu minh chung chung cua buoi hoc.
- Media nay duoc coi la bang chung hoat dong hoc tap cua nhom/lop.
- Neu can muc do ca nhan hoa cao hon, co the mo rong bang lien ket media theo hoc sinh.

Day la mot diem ban co the nhan manh la "co chu y mo rong sau nay".

---

## 6.6. AI trong he thong

AI xuat hien o 2 cho:

1. Rewrite/Refine note
2. Phan tich video -> tao `AiEvaluation`

Service AI dang dung:

- `IAIService`
- `GeminiAiService`

### Vai tro cua AI

AI khong thay the giao vien.
AI dong vai tro:

- ho tro viet ghi chu ro hon
- tong hop/phan tich minh chung
- tao them gia tri cho payroll/compliance

### Cach phan bien neu bi hoi

Hoi: "Tai sao can AI?"

Tra loi goi y:

- Khong phai de thay giao vien cham bai.
- Ma de:
  - chuan hoa chat luong ghi chu
  - ho tro tong hop du lieu nhanh
  - tao co so cho kiem tra tinh day du cua minh chung

Hoi: "Neu AI sai thi sao?"

Tra loi goi y:

- AI chi la lop ho tro.
- Quyet dinh nghiep vu cuoi cung van thuoc giao vien/admin.
- He thong van luu `TeacherRawNote` goc va co the kiem tra thu cong.

---

## 6.7. Day thay giao vien

He thong ho tro `SubstituteTeacherId` o cap `Session`.

### Logic nghiep vu dung

- Neu `SubstituteTeacherId == null`
  - giao vien chinh la nguoi phu trach session
- Neu `SubstituteTeacherId == teacher X`
  - giao vien X la nguoi duoc day thay
  - giao vien chinh khong con duoc tinh session do vao dashboard/thong ke/nhieu quyen thao tac

### Day la mot diem da tung co loi

Da tung ton tai bat nhat:

- lich hien "da giao day thay"
- nhung dashboard van dem session do la "chua diem danh" cua giao vien chinh

Da sua bang cach thong nhat bo loc.

### Day la mot diem bao cao rat tot

Ban co the noi:

- Bai toan day thay khong chi la doi ten giao vien tren lich
- ma con anh huong den:
  - quyen chinh sua
  - thong ke dashboard
  - evidence queue
  - payroll

No cho thay he thong co xu ly nghiep vu thuc te, khong chi CRUD don gian.

---

## 6.8. Payroll

### Muc tieu

Tinh luong giao vien theo session thay vi tinh tay.

### Input chinh

- Session da hoc
- Teacher profile
- Course difficulty
- Pay rate config
- Attendance compliance
- Note/media/AI completeness

### Service

[PayrollCalculationService.cs](</d:/STEM/STEM.Web/Services/PayrollCalculationService.cs:1>)

### Bonus/Penalty hien tai

- Thuong chuyen can
- Thuong compliance day du
- Phat thieu note
- Phat thieu video
- Thuong AI tot

### Session valid/invalid/pending

`AttendanceWorkflowService` + `AttendanceIntegrityRules` quyet dinh `PayrollStatus`.

Y nghia:

- Khong phai day xong la duoc tinh luong
- Session phai du dieu kien nghiep vu

### Diem bao cao rat manh

Day la phan "ERP-like" nhat cua he thong.
Ban co the nhan manh:

- Diem danh khong dung rieng de diem danh
- no la dau vao cua payroll governance

### Cau hoi giang vien co the hoi

Hoi: "Tai sao payroll lai lien quan media va note?"

Tra loi goi y:

- Vi he thong xem media va note la bang chung hoan thanh nghiep vu day hoc.
- Neu khong co minh chung/ghi nhan, trung tam khong du co so kiem soat chat luong.

---

## 6.9. Tai chinh

Controller chinh:

- `FinanceController`

Du lieu:

- `Invoice`
- `Payment`

### Chuc nang chinh

- Tao va xem hoa don
- Ghi nhan thanh toan
- Dong bo trang thai hoa don
- Quan ly cong no

### Diem nghiep vu quan trong

Da xu ly mot loi quan trong:

- Hoa don `Voided` phai thuc su bi loai khoi nghiep vu tai chinh
- Khong duoc tiep tuc nhan payment
- Khong duoc tiep tuc tinh vao cong no

### Cach trinh bay

Ban co the noi:

- He thong co theo duoi tinh nhat quan tai chinh, khong chi doi status tren UI.
- Khi "huy hoa don", can sua ca:
  - validation
  - tong hop cong no
  - dong bo status
  - quy tac thu tien

---

## 6.10. Portal hoc sinh/phu huynh

`StudentPortalController` la diem cham cuoi cung voi nguoi dung hoc sinh/phu huynh.

Module con:

- Tong quan
- Lich hoc
- Nhan xet & minh chung
- Hoc phi
- Ho so ca nhan

### Y nghia cua portal

No la lop "trinh bay gia tri":

- Giao vien va admin nhap du lieu
- Hoc sinh/phu huynh nhin thay ket qua

Neu khong co portal nay, he thong chi la mot he thong noi bo.

---

## 7. Cac diem manh de trinh bay voi giang vien

## 7.1. Khong phai CRUD don gian

He thong co nhieu rule nghiep vu that:

- day thay giao vien
- diem danh theo session
- minh chung hoc tap
- payroll theo compliance
- hoa don va payment
- muon/tra thiet bi

## 7.2. Du lieu lien thong giua cac module

Mot session anh huong den:

- lich hoc
- diem danh
- media/evidence
- AI evaluation
- payroll
- student portal

Day la diem rat de thuyet phuc.

## 7.3. Phan quyen theo role ro rang

- Admin / Teacher / Student
- Moi role co area rieng
- Moi role co use case rieng

## 7.4. Co business governance

He thong khong phai "muon nhap sao thi nhap".
No co:

- lock sua diem danh theo thoi gian/ky luong
- approved payroll lock
- void invoice rule
- validate media

---

## 8. Han che hien tai va cach tra loi

Ban nen noi truoc diem yeu. Nhu vay se tao cam giac ban hieu he thong, khong phai chi "demo cho chay".

## 8.1. Business logic con dan trong controller

Tinh trang:

- Mot so controller xu ly nghiep vu kha day

Tra loi goi y:

- Em uu tien chay duoc nghiep vu truoc
- Sau do em da tach dan cac phan co rule sang service nhu:
  - `AttendanceWorkflowService`
  - `PayrollCalculationService`
- Buoc tiep theo la tach sau hon de giam phu thuoc controller

## 8.2. File view co dau hieu loi encoding

Tinh trang:

- Mot so file `.cshtml` dang co chuoi mojibake

Tra loi goi y:

- Day la van de ky thuat ve encoding/chuoi unicode trong qua trinh patch file
- Khong lam sai nghiep vu, nhung anh huong den do sach code va can duoc chuan hoa UTF-8

## 8.3. File storage dang la local

Tinh trang:

- Hien tai dung `LocalFileStorageService`

Tra loi goi y:

- Phu hop cho moi truong do an/demo/noi bo
- Neu deploy production quy mo lon thi co the doi sang cloud object storage
- Interface da co (`IFileStorageService`) nen chi phi chuyen doi khong qua lon

## 8.4. AI chua phai co che "trustworthy AI" day du

Tinh trang:

- AI la lop ho tro, chua co dashboard evaluate/benchmark rieng

Tra loi goi y:

- He thong dat AI o vai tro tro ly, khong dat AI o vai tro phe duyet cuoi
- Quy tac nghiep vu van duoc rao boi con nguoi va validation

## 8.5. Chua co test tu dong day du

Neu giang vien hoi:

Tra loi goi y:

- Em da uu tien nghiep vu va integration flow truoc
- Huong nang cap tiep theo la viet test cho:
  - payroll rules
  - substitute teacher rules
  - finance void/payment rules
  - student evidence aggregation

---

## 9. Cac thay doi/ra soat quan trong da lam gan day

Ban co the dung muc nay de chung minh ban khong chi "build lung tung", ma co kha nang audit va cai tien.

### 9.1. Repo cleanup

- Da loai bo file rac va dependency cu khong con dung
- Da bo `CloudinaryStorageService` cu neu he thong khong con DI den no

### 9.2. Role va redirect

- Da canonicalize role de tranh lech `Teacher/Giao vien`, `Student/Hoc sinh`

### 9.3. Payroll va finance

- Da siet rule payroll
- Da sua logic invoice void
- Da dam bao giao vien thieu `TeacherProfile` van khong bi "mat" khoi bang luong

### 9.4. Day thay

- Da sua dashboard va mot so luong teacher de giao vien chinh khong bi tinh session da giao day thay

### 9.5. Evidence

- Da noi media cap session vao portal hoc sinh
- Da sua wording de dung nghiep vu hon

---

## 10. Cau hoi phan bien giang vien co the hoi va cau tra loi goi y

## 10.1. "He thong nay moi me o diem nao?"

Tra loi:

- Diem moi khong nam o cong nghe qua la, ma nam o viec so hoa lien thong nghiep vu.
- Tu diem danh va media co the di den payroll, evidence va portal hoc sinh.
- AI duoc dat dung vai tro ho tro nghiep vu thay vi lam cho vui.

## 10.2. "Tai sao khong lam monolithic CRUD don gian?"

Tra loi:

- Vi bai toan trung tam STEM co luong nghiep vu giao nhau.
- Neu chi CRUD thi khong giai quyet duoc:
  - day thay
  - payroll theo compliance
  - media minh chung
  - cong no va payment

## 10.3. "Tai sao khong tach microservice?"

Tra loi:

- Quy mo do an/chuc nang hien tai chua can microservice.
- Monolith co module ro rang giup:
  - de phat trien nhanh
  - de debug
  - de demo
- Da co tach theo area/service, nen ve sau van co the trich module neu can.

## 10.4. "Tai sao session la trung tam?"

Tra loi:

- Session la don vi van hanh thuc te:
  - co ngay, gio, phong, giao vien
  - co diem danh
  - co minh chung
  - co tinh luong
- Neu khong lay session lam trung tam thi cac module se roi rac.

## 10.5. "Neu 1 buoi co nhieu hoc sinh thi media chung xu ly the nao?"

Tra loi:

- Hien tai he thong co hai tang media:
  - media ca nhan
  - media chung cua buoi
- Media chung duoc luu theo session va phan bo cho hoc sinh trong buoi o portal.
- Neu can ca nhan hoa hon nua, co the bo sung mapping media theo hoc sinh.

## 10.6. "Neu giang vien khong nhap nhan xet thi sao?"

Tra loi:

- He thong van cho phep ton tai session co media ma chua co ghi chu.
- Tuy nhien nghiep vu payroll/compliance co co che phat/anh huong theo muc do day du.
- He thong khong bat buoc phai co "nhan xet chung ca buoi".

---

## 11. Cach tu gioi thieu he thong trong 3-5 phut

Ban co the noi theo khung sau:

1. Bai toan:
   - Em xay dung he thong quan ly trung tam STEM, khong chi quan ly hoc sinh ma con quan ly van hanh day hoc, minh chung, tai chinh va luong.

2. Kien truc:
   - He thong dung ASP.NET Core MVC, SQL Server, EF Core, chia area theo Admin, Teacher, Student.

3. Data model:
   - Course -> Class -> Session la xương song. Session la don vi nghiep vu trung tam.

4. Luong chinh:
   - Admin tao khoa hoc, lop, lich.
   - Giao vien diem danh, nhap ghi chu, upload minh chung.
   - Hoc sinh/phu huynh xem portal.
   - He thong tong hop du lieu de tinh payroll va quan ly hoc phi.

5. Diem dac biet:
   - Ho tro giao vien day thay
   - Media minh chung cap ca nhan va cap buoi
   - AI ho tro tong hop/chuẩn hoa ghi chu
   - Payroll phu thuoc muc do hoan thanh nghiep vu

6. Han che va huong mo rong:
   - Can tiep tuc tach business logic, bo sung test va chuan hoa storage/encoding.

---

## 12. Cach tra loi neu bi hoi "em co that su hieu he thong cua em khong?"

Ban nen nho 5 cau nay:

1. Session la don vi trung tam cua he thong.
2. Nhan xet hien tai la nhan xet theo tung hoc sinh, khong phai nhan xet chung cua ca buoi.
3. Media co 2 loai: media ca nhan va media chung cua session.
4. Day thay khong chi doi giao vien tren UI, ma phai doi ca quyen, thong ke va payroll.
5. Payroll duoc xay tren du lieu compliance, khong phai chi dem so buoi day.

Neu ban noi duoc 5 y nay mach lac, giang vien se thay ban thuc su hieu he thong.

---

## 13. De xuat huong nang cap neu giang vien hoi "neu lam tiep em se lam gi?"

Ban co the tra loi:

### Huong 1. Lam sach kien truc

- Tiep tuc tach business logic khoi controller
- Tao service/facade cho:
  - finance
  - student evidence
  - substitute workflow

### Huong 2. Tang do tin cay

- Viet test cho payroll, finance, evidence aggregation
- Tao regression checklist

### Huong 3. Tang kha nang production

- Chuyen file storage sang cloud
- Them logging/audit ro hon
- Hardening AI workflow

### Huong 4. Tang chat luong nghiep vu

- Phan biet ro minh chung ca nhan va minh chung chung trong UI
- Them dashboard compliance cho admin
- Them report theo giao vien/lop/khoa hoc

---

## 14. Ket luan ngan gon de ban nho

He thong STEM nay la mot he thong quan ly trung tam hoc tap theo huong van hanh thuc te. No khong chi quan ly course va hoc sinh, ma con lien thong session, diem danh, media minh chung, AI, payroll, hoc phi va student portal. Gia tri cua he thong nam o viec bien du lieu day hoc thanh du lieu van hanh co the kiem soat, do luong va bao cao.

Neu can phan bien voi giang vien, hay quay ve 3 y cot loi:

- Kien truc hop ly cho bai toan hien tai
- Session la trung tam nghiep vu
- He thong co xu ly rule thuc te, khong chi la CRUD
