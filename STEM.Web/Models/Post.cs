using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class Post
{
    public int Id { get; set; }

    public int AuthorId { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    /// <summary>Mô tả ngắn hiển thị ở danh sách bài viết (tối đa 500 ký tự)</summary>
    public string? Excerpt { get; set; }

    /// <summary>Danh mục bài viết, ví dụ: STEM, Công nghệ, Giáo dục</summary>
    public string? Category { get; set; }

    /// <summary>URL ảnh thumbnail của bài viết</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Thời điểm đăng bài (dùng để hiển thị ngày đăng)</summary>
    public DateTime? PublishedAt { get; set; }

    public string Content { get; set; } = null!;

    public bool IsPublished { get; set; }

    public virtual User Author { get; set; } = null!;
}
