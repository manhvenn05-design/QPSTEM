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
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string PublishedAtText { get; set; } = string.Empty;
}

public class BannerManagementItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
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
    [Display(Name = "Slug (đường dẫn)")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả ngắn tối đa 500 ký tự.")]
    [Display(Name = "Mô tả ngắn")]
    public string? Excerpt { get; set; }

    [StringLength(100, ErrorMessage = "Danh mục tối đa 100 ký tự.")]
    [Display(Name = "Danh mục")]
    public string? Category { get; set; }

    [Display(Name = "Ảnh thumbnail")]
    public IFormFile? ThumbnailFile { get; set; }

    /// <summary>URL ảnh thumbnail hiện tại (dùng khi edit)</summary>
    public string? CurrentImageUrl { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung.")]
    [Display(Name = "Nội dung (HTML)")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Hiển thị ngay")]
    public bool IsPublished { get; set; }

    public IReadOnlyList<SelectListItem> AuthorOptions { get; set; } = [];

    /// <summary>Gợi ý danh mục đã có trong DB</summary>
    public IReadOnlyList<string> CategorySuggestions { get; set; } = [];
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
    public string? Excerpt { get; set; }
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string PublishedAtText { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
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
    [Display(Name = "Liên kết khi click")]
    public string? LinkUrl { get; set; }

    [Display(Name = "Đang hiển thị")]
    public bool IsActive { get; set; }

    [Display(Name = "Thứ tự hiển thị")]
    [Range(0, 999, ErrorMessage = "Thứ tự từ 0 đến 999.")]
    public int SortOrder { get; set; }
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
    public int SortOrder { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}
