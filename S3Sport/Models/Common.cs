namespace S3Sport.Models;

/// <summary>Base class for all domain entities: integer key and audit timestamps (UTC).</summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Entities that are hidden instead of physically deleted. Enforced by a global query filter.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string TeamManager = "TeamManager";
    public const string TournamentManager = "TournamentManager";
    public const string MatchOperator = "MatchOperator";
    public const string ContentManager = "ContentManager";
    public const string User = "User";

    public static readonly string[] All =
        [SuperAdmin, Admin, TeamManager, TournamentManager, MatchOperator, ContentManager, User];

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        [SuperAdmin] = "مدير النظام الأعلى",
        [Admin] = "مدير المنصة",
        [TeamManager] = "مسؤول فريق",
        [TournamentManager] = "مسؤول بطولة",
        [MatchOperator] = "مسؤول مباراة",
        [ContentManager] = "مسؤول المحتوى",
        [User] = "مستخدم"
    };
}

public static class AppPolicies
{
    /// <summary>SuperAdmin, Admin</summary>
    public const string AdminAccess = "AdminAccess";
    /// <summary>SuperAdmin only (roles and user permissions)</summary>
    public const string SuperAdminOnly = "SuperAdminOnly";
    /// <summary>Admins + TeamManager (ownership checked per team in controllers)</summary>
    public const string ManageTeams = "ManageTeams";
    /// <summary>Admins + TournamentManager (ownership checked per competition)</summary>
    public const string ManageCompetitions = "ManageCompetitions";
    /// <summary>Admins + TournamentManager + MatchOperator (assignment checked per match)</summary>
    public const string OperateMatches = "OperateMatches";
    /// <summary>Admins + ContentManager</summary>
    public const string ManageContent = "ManageContent";
}
