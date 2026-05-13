# AI Stability Implementation Plan

Ngày tạo: `2026-05-13`

Mục tiêu của tài liệu này:

- Ghi lại kế hoạch và hướng dẫn triển khai lại phần AI cho project `STEM.Web`
- Tối ưu theo hướng ổn định, đủ dùng cho lượng người dùng ít
- Tránh tình trạng `hôm nay chạy được, mai lại lỗi`
- Dùng làm tài liệu handoff cho AI agent khác đọc và tiếp tục thực hiện

---

## 1. Bối cảnh hiện tại

Phần AI hiện tại đang nằm chủ yếu ở:

- `STEM.Web/Areas/Teacher/Controllers/AICopilotController.cs`
- `STEM.Web/Areas/Teacher/Controllers/AttendancesController.cs`
- `STEM.Web/Areas/Teacher/Views/Attendances/Board.cshtml`
- `STEM.Web/appsettings.json`
- `TestGemini/Program.cs`

Các chức năng AI đang có:

- Viết lại ghi chú giáo viên bằng AI
- Phân tích video học sinh bằng AI
- Upload video rồi gửi tiếp sang Google AI

Vấn đề đã xảy ra:

- API key bị Google đánh dấu là leaked và trả về `403 PERMISSION_DENIED`
- Key đang để trong source code, không an toàn
- Luồng AI và upload còn thiếu lớp bảo vệ, giám sát và cơ chế fallback

---

## 2. Mục tiêu triển khai mới

Mục tiêu kỹ thuật:

- Không commit API key thật vào repo
- Không phụ thuộc vào 1 key theo kiểu "cắm là chạy"
- Có cấu trúc AI service rõ ràng, dễ thay thế và bảo trì
- Có quota control cơ bản cho lượng user nhỏ
- Có logging, retry và fallback hợp lý
- UI không bị chết theo AI

Mục tiêu nghiệp vụ:

- Giáo viên vẫn thao tác được ngay cả khi AI lỗi
- Tính năng AI chỉ là hỗ trợ, không được trở thành điểm nghẽn
- Kết quả AI phải dễ kiểm soát, dễ debug và an toàn hơn

---

## 3. Định hướng kiến trúc

### 3.1. Không xem API key là nền tảng

API key chỉ là credential, không phải giải pháp vận hành.

Phần AI ổn định phải gồm đủ các lớp:

- secret management
- AI service abstraction
- retry policy
- timeout policy
- request throttling
- logging/monitoring
- graceful fallback

### 3.2. Tách môi trường

Nên tách tối thiểu:

- `dev`
- `staging`
- `production`

Khuyến nghị:

- Mỗi môi trường dùng project Google riêng
- Không dùng chung quota và key giữa dev/test/prod

Lý do:

- Rate limit và quota được tính theo project
- Tách project giúp tránh việc test làm ảnh hưởng production

### 3.3. Chọn mức triển khai phù hợp

Với lượng người dùng ít, ưu tiên:

- bắt đầu với `Gemini Developer API`
- thiết kế theo chuẩn production nhỏ

Chỉ cân nhắc chuyển lên mức enterprise hơn khi:

- cần kiểm soát doanh nghiệp chặt hơn
- cần auth/service account chuẩn enterprise
- cần mở rộng nhiều hơn về lâu dài

---

## 4. Những việc bắt buộc phải sửa

### 4.1. Secret management

Phải làm:

- Gỡ API key khỏi `appsettings.json`
- Gỡ API key khỏi `TestGemini/Program.cs`
- Không để key trong bất kỳ file commit nào

Nên dùng:

- local dev: `User Secrets` hoặc environment variable
- server: environment variable hoặc secret store

Không nên:

- nhét key vào URL query string
- hardcode key trong controller
- copy key vào file test

### 4.2. Chuẩn hóa cách gọi AI

Phải làm:

- Không gọi AI trực tiếp rải rác từ nhiều nơi
- Tạo 1 service trung tâm, ví dụ:
  - `IAIService`
  - `GeminiAiService`

Service này chịu trách nhiệm:

- build request
- gửi request
- parse response
- xử lý timeout
- xử lý retry
- map lỗi sang message an toàn cho UI

### 4.3. Chia AI thành 2 nhóm use case

#### A. Note rewriting

Đặc điểm:

- nhanh
- ít tốn tài nguyên hơn
- user mong phản hồi gần realtime

Thiết kế:

- request đồng bộ
- timeout ngắn, ví dụ `8-15s`
- nếu lỗi thì trả message rõ ràng và cho user tiếp tục nhập thủ công

#### B. Video analysis

Đặc điểm:

- nặng
- nhiều bước
- dễ timeout
- dễ gặp quota/rate limit hơn

Thiết kế:

- không nên xử lý theo kiểu blocking trực tiếp trong 1 request UI dài
- nên tách thành luồng bất đồng bộ
- trạng thái gợi ý:
  - `pending`
  - `processing`
  - `done`
  - `failed`

Khuyến nghị:

- upload xong thì tạo job phân tích
- UI polling hoặc refresh trạng thái
- nếu lỗi vẫn giữ video và cho phép thử lại

---

## 5. Thiết kế service đề xuất

### 5.1. Interface đề xuất

Ví dụ hướng triển khai:

- `IAIService`
  - `Task<RefineNoteResult> RefineTeacherNoteAsync(...)`
  - `Task<VideoAnalysisResult> AnalyzePresentationVideoAsync(...)`

- `IAIRequestLimiter`
- `IAIUsageLogger`
- `IAIFallbackPolicy`

### 5.2. Config đề xuất

Tạo object config riêng, ví dụ:

- `GoogleAiOptions`
  - `ApiKey`
  - `TextModel`
  - `VideoModel`
  - `BaseUrl`
  - `RequestTimeoutSeconds`
  - `MaxRetryCount`
  - `EnableVideoAnalysis`
  - `EnableNoteRefine`

### 5.3. Nguyên tắc gọi API

Phải có:

- timeout rõ ràng
- retry có backoff cho lỗi tạm thời
- không retry với:
  - `400`
  - `401`
  - `403`

Nên có:

- request id
- correlation id
- logging model name và feature name

---

## 6. Quota và ổn định cho lượng user ít

### 6.1. Mục tiêu thực tế

Không cần tối ưu cho tải lớn.

Chỉ cần:

- chịu được vài giáo viên dùng đồng thời
- tránh burst request vô ý
- tránh lặp request vô nghĩa

### 6.2. Kiểm soát request trong app

Nên có:

- giới hạn số request AI trên mỗi user trong 1 khoảng thời gian
- giới hạn riêng cho video analysis
- cooldown ngắn cho nút AI để tránh click liên tục

Ví dụ hướng triển khai:

- note refine: `3-5 request / phút / user`
- video analysis: `1 request đang chạy / user`

### 6.3. Cache kết quả

Nên có cache hoặc lưu hash input để tránh gọi lặp:

- cùng 1 note mà bấm lại liên tục
- cùng 1 video mà phân tích lại liên tục

Gợi ý:

- note cache theo `hash(rawNote)`
- video cache theo `attendanceId + mediaUrl + updatedAt`

---

## 7. Logging và giám sát

### 7.1. Những gì cần log

Mỗi request AI nên log:

- `requestId`
- `userId`
- `feature`
- `model`
- `startTime`
- `durationMs`
- `statusCode`
- `isSuccess`
- `errorType`

### 7.2. Không log gì

Không log:

- API key
- raw prompt đầy đủ nếu có dữ liệu nhạy cảm
- toàn bộ video/url nội bộ nhạy cảm nếu không cần

### 7.3. Dashboard tối thiểu

Nên theo dõi ít nhất:

- tổng số request AI
- số request thành công
- số lỗi `403`
- số lỗi `429`
- latency trung bình
- số lần retry

---

## 8. Fallback và trải nghiệm người dùng

Nguyên tắc:

- AI chỉ hỗ trợ, không được khóa luồng nghiệp vụ chính

### 8.1. Với note rewrite

Nếu AI lỗi:

- vẫn giữ nguyên note thô
- báo lỗi gọn
- cho user tiếp tục lưu attendance bình thường

### 8.2. Với video analysis

Nếu AI lỗi:

- vẫn giữ video đã upload
- đánh dấu trạng thái `failed`
- cho user bấm thử lại
- không làm mất dữ liệu attendance khác

### 8.3. Message lỗi

UI chỉ nên hiện:

- lỗi cấu hình
- lỗi quota
- lỗi tạm thời
- lỗi upload

Không nên hiện:

- raw response dài dòng từ provider
- exception nội bộ

---

## 9. Bảo mật bắt buộc

### 9.1. API key

Phải làm:

- rotate key định kỳ
- giới hạn key theo API
- lưu key trong secret

### 9.2. Upload file

Phải làm:

- validate MIME type ở server
- whitelist extension
- kiểm soát path
- không tin `VideoUrl` từ client

### 9.3. CSRF

Phải làm:

- thêm anti-forgery token cho các POST endpoint liên quan AI/upload
- gửi token trong `fetch`

### 9.4. XSS

Phải làm:

- bỏ `innerHTML` nếu dữ liệu đến từ AI
- dùng `textContent` hoặc encode output

---

## 10. Hướng cải tổ mã nguồn

### 10.1. Những gì nên tách khỏi controller

Nên tách:

- gọi Google API
- parse response
- xử lý upload metadata
- retry logic
- error mapping

Controller chỉ nên:

- nhận request
- validate đầu vào
- gọi service
- trả response cho UI

### 10.2. Cấu trúc thư mục gợi ý

Ví dụ:

- `STEM.Web/Services/AI/IAIService.cs`
- `STEM.Web/Services/AI/GeminiAiService.cs`
- `STEM.Web/Services/AI/GoogleAiOptions.cs`
- `STEM.Web/Services/AI/AIRequestResult.cs`
- `STEM.Web/Services/Storage/IVideoStorageService.cs`
- `STEM.Web/Services/Storage/LocalVideoStorageService.cs`

### 10.3. Những gì nên bỏ khỏi repo hoặc cô lập

Nên dọn:

- project test có key thật
- file error/log không cần thiết
- code thử nghiệm không phục vụ production

---

## 11. Checklist triển khai cho AI agent

### Phase 1: Secure baseline

- Xóa key khỏi source
- Đọc key từ env var / secret
- Đổi key mới
- Gỡ endpoint/test code nguy hiểm

### Phase 2: Service abstraction

- Tạo `IAIService`
- Chuyển logic AI từ controller sang service
- Dùng config typed options

### Phase 3: Stability controls

- timeout
- retry có backoff
- logging
- request limiter
- fallback message

### Phase 4: Video flow hardening

- siết upload
- chống path traversal
- tách trạng thái xử lý video
- cho phép retry an toàn

### Phase 5: UI cleanup

- bỏ hiển thị lỗi thô
- bỏ render `innerHTML` từ AI
- cải thiện trạng thái loading / failed / retry

---

## 12. Các quyết định nên giữ nhất quán

- Không để AI chặn nghiệp vụ chính
- Không để secret trong repo
- Không dùng query string để mang API key
- Không tăng quota bằng cách tạo nhiều key trong cùng 1 project
- Không để video analysis chạy đồng bộ dài dòng nếu có thể tránh

---

## 13. Những thông tin AI agent có thể cần từ người dùng

Nếu AI agent cần triển khai đầy đủ, có thể sẽ cần thêm:

- API key mới
- môi trường deploy hiện tại
- server chạy ở đâu
- có dùng IIS hay không
- có chấp nhận dùng environment variable hay user secrets hay không
- có muốn giữ Gemini Developer API hay đổi hướng triển khai khác
- mức tải dự kiến:
  - bao nhiêu giáo viên
  - mỗi ngày bao nhiêu lần note refine
  - mỗi ngày bao nhiêu video analysis
- có cần lưu lịch sử usage AI để báo cáo không

---

## 14. Câu nhắn cho AI agent tiếp theo

Bạn hãy dùng tài liệu này làm nguồn định hướng chính để cải tổ phần AI của project.

Ưu tiên theo thứ tự:

- bảo mật
- ổn định
- đúng kiến trúc
- tiết kiệm request
- giữ nguyên nghiệp vụ chính khi AI lỗi

Nếu cần tôi cung cấp thông tin gì cứ nói là tôi sẽ tìm và cung cấp cho AI agent.

