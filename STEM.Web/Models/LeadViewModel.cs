using System.ComponentModel.DataAnnotations;

namespace STEM.Web.Models;

public class LeadViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên phụ huynh/học sinh.")]
    [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
    public string? ParentName { get; init; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại để trung tâm liên hệ.")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
    public string? Phone { get; init; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(150, ErrorMessage = "Email không được vượt quá 150 ký tự.")]
    public string? Email { get; init; }

    public int? InterestedId { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý với điều khoản dịch vụ.")]
    public bool AcceptTerms { get; init; }
}
