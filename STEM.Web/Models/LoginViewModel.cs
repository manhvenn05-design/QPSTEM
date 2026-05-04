using System.ComponentModel.DataAnnotations;

namespace STEM.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email")]
    [Display(Name = "Tên đăng nhập hoặc Email")]
    public string? UsernameOrEmail { get; init; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string? Password { get; init; }

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; init; }
}
