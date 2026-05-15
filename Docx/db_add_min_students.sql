-- ============================================================
-- Thêm cột MinStudents vào bảng Courses
-- Mô tả: Số học viên tối thiểu để mở một buổi học của khóa này.
--        Mặc định là 0 = không giới hạn (bỏ qua kiểm tra).
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Courses' AND COLUMN_NAME = 'MinStudents'
)
BEGIN
    ALTER TABLE Courses
    ADD MinStudents INT NOT NULL DEFAULT 0;

    PRINT 'Da them cot MinStudents vao bang Courses thanh cong.';
END
ELSE
BEGIN
    PRINT 'Cot MinStudents da ton tai, bo qua.';
END
