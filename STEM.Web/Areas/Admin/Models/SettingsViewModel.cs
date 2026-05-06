namespace STEM.Web.Areas.Admin.Models;

public class SettingsViewModel
{
    public int TotalUsers { get; set; }
    public int ActiveTeachers { get; set; }
    public int ActiveClasses { get; set; }
    public int UnpaidInvoices { get; set; }
    public int EquipmentsInMaintenance { get; set; }
    public int PublishedPosts { get; set; }
    public int ActiveBanners { get; set; }
}
