using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace STEM.Web.Areas.Admin.Models;

public class CmsManagementViewModel
{
    public string SelectedFilter { get; set; } = "all";
    public string SearchTerm { get; set; } = string.Empty;
    public int TotalPosts { get; set; }
    public int PublishedPosts { get; set; }
    public int TotalBanners { get; set; }
    public int ActiveBanners { get; set; }
    public IReadOnlyList<CmsFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<PostManagementItemViewModel> Posts { get; set; } = [];
    public IReadOnlyList<BannerManagementItemViewModel> Banners { get; set; } = [];
}

public class CmsFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class PostManagementItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class BannerManagementItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool IsActive { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class CreatePostViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn tác giả.")]
    [Display(Name = "Tác giả")]
    public int? AuthorId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề.")]
    [StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập slug.")]
    [StringLength(200, ErrorMessage = "Slug tối đa 200 ký tự.")]
    [Display(Name = "Slug")]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung.")]
    [Display(Name = "Nội dung")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Hiển thị ngay")]
    public bool IsPublished { get; set; }

    public IReadOnlyList<SelectListItem> AuthorOptions { get; set; } = [];
}

public class EditPostViewModel : CreatePostViewModel
{
    public int Id { get; set; }
}

public class PostDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class CreateBannerViewModel
{
    [StringLength(100, ErrorMessage = "Tiêu đề tối đa 100 ký tự.")]
    [Display(Name = "Tiêu đề")]
    public string? Title { get; set; }

    [Display(Name = "Ảnh banner")]
    public IFormFile? ImageFile { get; set; }

    public string? ImageUrl { get; set; }

    [StringLength(500, ErrorMessage = "Liên kết tối đa 500 ký tự.")]
    [Display(Name = "Liên kết")]
    public string? LinkUrl { get; set; }

    [Display(Name = "Đang hiển thị")]
    public bool IsActive { get; set; }
}

public class EditBannerViewModel : CreateBannerViewModel
{
    public int Id { get; set; }
    public string? CurrentImageUrl { get; set; }
}

public class BannerDetailsViewModel
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool IsActive { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}
