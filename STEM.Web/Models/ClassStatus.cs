using System.ComponentModel.DataAnnotations;

namespace STEM.Web.Models;

public enum ClassStatus
{
    [Display(Name = "Đang diễn ra")]
    Active = 0,

    [Display(Name = "Đã kết thúc")]
    Completed = 1,

    [Display(Name = "Đã hủy")]
    Cancelled = 2,

    [Display(Name = "Tạm dừng")]
    Suspended = 3
}
