using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace STEM.Web.Models;

public partial class Room
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;

    public int Capacity { get; set; }

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
}
