using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace S3Sport.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(150)] public string FullName { get; set; } = string.Empty;
    [StringLength(300)] public string? ProfileImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<UserArea> UserAreas { get; set; } = new List<UserArea>();
    public ICollection<TeamMember> TeamMemberships { get; set; } = new List<TeamMember>();
    public ICollection<UserFollow> Follows { get; set; } = new List<UserFollow>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
