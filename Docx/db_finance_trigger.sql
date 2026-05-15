-- =========================================================================================
-- TRIGGER CHẶN TỔNG THANH TOÁN VƯỢT QUÁ CÔNG NỢ
-- =========================================================================================

IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_Payments_CheckAmount')
BEGIN
    DROP TRIGGER TR_Payments_CheckAmount;
    PRINT 'Đã xóa trigger cũ TR_Payments_CheckAmount.';
END
GO

CREATE TRIGGER TR_Payments_CheckAmount
ON Payments
AFTER INSERT, UPDATE
AS
BEGIN
    -- Chỉ kiểm tra nếu có bản ghi bị ảnh hưởng
    IF @@ROWCOUNT = 0 RETURN;

    -- Kiểm tra xem có Hóa đơn nào bị vi phạm không (Tổng thanh toán > FinalAmount)
    IF EXISTS (
        SELECT i.Id
        FROM Invoices i
        INNER JOIN (
            -- Lấy tất cả Hóa đơn bị ảnh hưởng trong đợt cập nhật này
            SELECT DISTINCT InvoiceId 
            FROM inserted
        ) updated_invoices ON i.Id = updated_invoices.InvoiceId
        -- Join để tính tổng số tiền của Hóa đơn đó
        INNER JOIN (
            SELECT InvoiceId, SUM(Amount) AS TotalPaid
            FROM Payments
            GROUP BY InvoiceId
        ) payment_totals ON i.Id = payment_totals.InvoiceId
        -- Điều kiện vi phạm: Tổng thu > Số tiền phải thu
        WHERE payment_totals.TotalPaid > i.FinalAmount
    )
    BEGIN
        -- Nếu phát hiện vi phạm, báo lỗi và Rollback Transaction
        RAISERROR ('Lỗi Dữ liệu: Tổng số tiền thanh toán không được vượt quá giá trị (FinalAmount) của hóa đơn.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
END
GO

PRINT 'Đã tạo trigger TR_Payments_CheckAmount thành công.';
GO
