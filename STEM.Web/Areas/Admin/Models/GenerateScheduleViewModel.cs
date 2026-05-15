using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace STEM.Web.Areas.Admin.Models;

public class GenerateScheduleViewModel
{
    [Required]
    public int ClassId { get; set; }

    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int TotalSessionsAllowed { get; set; }
    public int CurrentSessionCount { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phòng học.")]
    [Display(Name = "Phòng học")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu sinh lịch.")]
    [Display(Name = "Ngày bắt đầu")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
    [Display(Name = "Giờ bắt đầu")]
    public TimeOnly StartTime { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc.")]
    [Display(Name = "Giờ kết thúc")]
    public TimeOnly EndTime { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số buổi muốn sinh.")]
    [Range(1, 100, ErrorMessage = "Số buổi phải từ 1 đến 100.")]
    [Display(Name = "Số lượng buổi")]
    public int SessionCount { get; set; }

    [Display(Name = "Thứ trong tuần")]
    [Required(ErrorMessage = "Vui lòng chọn ít nhất 1 ngày trong tuần.")]
    public List<DayOfWeek> SelectedDays { get; set; } = new();

    public List<SelectListItem> RoomOptions { get; set; } = new();
    
    public List<SelectListItem> DayOptions { get; set; } = new()
    {
        new SelectListItem("Thứ 2", DayOfWeek.Monday.ToString()),
        new SelectListItem("Thứ 3", DayOfWeek.Tuesday.ToString()),
        new SelectListItem("Thứ 4", DayOfWeek.Wednesday.ToString()),
        new SelectListItem("Thứ 5", DayOfWeek.Thursday.ToString()),
        new SelectListItem("Thứ 6", DayOfWeek.Friday.ToString()),
        new SelectListItem("Thứ 7", DayOfWeek.Saturday.ToString()),
        new SelectListItem("Chủ Nhật", DayOfWeek.Sunday.ToString())
    };
}
