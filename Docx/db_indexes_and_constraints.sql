-- =========================================================================================
-- QPSTEM Database Optimization Script
-- Thêm Indexes để tăng tốc độ truy vấn & Unique Constraints để đảm bảo toàn vẹn dữ liệu
-- =========================================================================================

-- 1. Thêm Unique Constraint cho Enrollments
-- Đảm bảo 1 học sinh chỉ có thể đăng ký 1 lớp duy nhất 1 lần (không bị ghi đè/trùng lặp)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_Enrollment_Student_Class' AND object_id = OBJECT_ID('Enrollments'))
BEGIN
    ALTER TABLE Enrollments 
    ADD CONSTRAINT UQ_Enrollment_Student_Class UNIQUE (StudentId, ClassId);
    PRINT 'Đã thêm Unique Constraint cho bảng Enrollments.';
END
ELSE
BEGIN
    PRINT 'Unique Constraint UQ_Enrollment_Student_Class đã tồn tại.';
END
GO

-- 2. Thêm Index cho bảng Sessions
-- Tối ưu hóa truy vấn khi lọc danh sách buổi học theo ngày (VD: Lịch học hôm nay)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Sessions_Date' AND object_id = OBJECT_ID('Sessions'))
BEGIN
    CREATE INDEX IX_Sessions_Date ON Sessions(Date);
    PRINT 'Đã thêm Index IX_Sessions_Date.';
END
ELSE
BEGIN
    PRINT 'Index IX_Sessions_Date đã tồn tại.';
END
GO

-- Tối ưu hóa truy vấn khi kiểm tra trùng lịch hoặc lấy danh sách buổi học của một lớp
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Sessions_ClassId_Date' AND object_id = OBJECT_ID('Sessions'))
BEGIN
    CREATE INDEX IX_Sessions_ClassId_Date ON Sessions(ClassId, Date);
    PRINT 'Đã thêm Index IX_Sessions_ClassId_Date.';
END
ELSE
BEGIN
    PRINT 'Index IX_Sessions_ClassId_Date đã tồn tại.';
END
GO

-- 3. Thêm Index cho bảng Attendances
-- Tối ưu hóa truy vấn khi xem lại lịch sử điểm danh của một học sinh
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Attendances_StudentId' AND object_id = OBJECT_ID('Attendances'))
BEGIN
    CREATE INDEX IX_Attendances_StudentId ON Attendances(StudentId);
    PRINT 'Đã thêm Index IX_Attendances_StudentId.';
END
ELSE
BEGIN
    PRINT 'Index IX_Attendances_StudentId đã tồn tại.';
END
GO
