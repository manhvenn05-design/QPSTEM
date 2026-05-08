using System.ComponentModel.DataAnnotations;

namespace STEM.Web.Areas.Admin.Models;

public class ConvertLeadViewModel
{
    public int LeadId { get; set; }

    [Display(Name = "Họ tên Phụ huynh")]
    public string? ParentName { get; set; }

    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên học viên")]
    [Display(Name = "Họ tên Học viên")]
    public string StudentName { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [Display(Name = "Tên đăng nhập (Tự tạo)")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "123456aA@"; // Mật khẩu mặc định

    [Display(Name = "Chọn lớp học (Ghi danh ngay)")]
    public int? SelectedClassId { get; set; }
}
