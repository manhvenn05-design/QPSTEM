using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class Post
{
    public int Id { get; set; }

    public int AuthorId { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string Content { get; set; } = null!;

    public bool IsPublished { get; set; }

    public virtual User Author { get; set; } = null!;
}
