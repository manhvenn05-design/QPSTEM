-- ==========================================
-- PHẦN 3: LƯU VẾT THỜI GIAN (AUDIT TRAIL)
-- ==========================================

-- 1. Thêm Audit cho bảng Classes
ALTER TABLE Classes ADD CreatedAt DATETIME NOT NULL DEFAULT GETDATE();
ALTER TABLE Classes ADD UpdatedAt DATETIME NULL;
GO

-- 2. Thêm Audit cho bảng Sessions
ALTER TABLE Sessions ADD CreatedAt DATETIME NOT NULL DEFAULT GETDATE();
ALTER TABLE Sessions ADD UpdatedAt DATETIME NULL;
GO
