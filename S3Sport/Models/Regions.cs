using System.ComponentModel.DataAnnotations;

namespace S3Sport.Models;

// Hierarchy: Governorate (محافظة) > Wilayat (ولاية) > Area (منطقة) > Village (قرية)

public class Governorate : BaseEntity
{
    [Required, StringLength(100)] public string NameAr { get; set; } = string.Empty;
    [Required, StringLength(100)] public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Wilayat> Wilayats { get; set; } = new List<Wilayat>();
}

public class Wilayat : BaseEntity
{
    public int GovernorateId { get; set; }
    public Governorate Governorate { get; set; } = null!;

    [Required, StringLength(100)] public string NameAr { get; set; } = string.Empty;
    [Required, StringLength(100)] public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Area> Areas { get; set; } = new List<Area>();
}

public class Area : BaseEntity
{
    public int WilayatId { get; set; }
    public Wilayat Wilayat { get; set; } = null!;

    [Required, StringLength(100)] public string NameAr { get; set; } = string.Empty;
    [Required, StringLength(100)] public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Village> Villages { get; set; } = new List<Village>();
    public ICollection<TeamArea> TeamAreas { get; set; } = new List<TeamArea>();
}

public class Village : BaseEntity
{
    public int AreaId { get; set; }
    public Area Area { get; set; } = null!;

    [Required, StringLength(100)] public string NameAr { get; set; } = string.Empty;
    [Required, StringLength(100)] public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A region selected by a user. The deepest non-null level is the effective filter
/// (e.g. only GovernorateId set = whole governorate). Exactly one row per user may be IsDefault.
/// </summary>
public class UserArea : BaseEntity
{
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int GovernorateId { get; set; }
    public Governorate Governorate { get; set; } = null!;
    public int? WilayatId { get; set; }
    public Wilayat? Wilayat { get; set; }
    public int? AreaId { get; set; }
    public Area? Area { get; set; }
    public int? VillageId { get; set; }
    public Village? Village { get; set; }

    public bool IsDefault { get; set; }
}
