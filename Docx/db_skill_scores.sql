-- =========================================================================================
-- BÓC TÁCH JSON SOFT SKILL SANG TABLE ĐỘC LẬP
-- =========================================================================================

-- 1. Tạo bảng AttendanceSkillScores
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AttendanceSkillScores')
BEGIN
    CREATE TABLE AttendanceSkillScores (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AttendanceId INT NOT NULL,
        SkillName NVARCHAR(100) NOT NULL,
        Score INT NOT NULL CHECK (Score >= 1 AND Score <= 10),
        Feedback NVARCHAR(1000) NULL,
        CONSTRAINT FK_AttendanceSkillScores_Attendances FOREIGN KEY (AttendanceId) 
            REFERENCES Attendances(Id) ON DELETE CASCADE
    );
    
    CREATE INDEX IX_AttendanceSkillScores_AttendanceId ON AttendanceSkillScores(AttendanceId);
    
    PRINT 'Đã tạo bảng AttendanceSkillScores thành công.';
END
ELSE
BEGIN
    PRINT 'Bảng AttendanceSkillScores đã tồn tại.';
END
GO

-- 2. Xóa cột SoftSkillJson cũ trong bảng Attendances (Sau khi backup dữ liệu nếu có)
-- Lưu ý: Vì hiện tại hệ thống chưa có dữ liệu SoftSkillJson thật (chỉ mới là string rỗng hoặc null),
-- ta có thể drop cột này luôn để Database sạch sẽ.
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Attendances') AND name = 'SoftSkillJson')
BEGIN
    ALTER TABLE Attendances DROP COLUMN SoftSkillJson;
    PRINT 'Đã xóa cột SoftSkillJson khỏi bảng Attendances.';
END
ELSE
BEGIN
    PRINT 'Cột SoftSkillJson không còn tồn tại.';
END
GO
