using System.ComponentModel.DataAnnotations;

namespace S3Sport.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "تذكرني")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "الاسم يجب أن يكون بين 3 و150 حرفاً")]
    [Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
    [StringLength(150)]
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
    [StringLength(20)]
    [Display(Name = "رقم الهاتف (اختياري)")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "كلمة المرور يجب ألا تقل عن 8 أحرف")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "كلمتا المرور غير متطابقتين")]
    [Display(Name = "تأكيد كلمة المرور")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

/// <summary>
/// Sign-up as a team: the account (manager) plus the team application in one form.
/// The account becomes the team manager once the platform approves the application.
/// </summary>
public class RegisterTeamViewModel : TeamApplicationFormViewModel
{
    [Required(ErrorMessage = "اسم مسؤول الفريق مطلوب")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "الاسم يجب أن يكون بين 3 و150 حرفاً")]
    [Display(Name = "اسم مسؤول الفريق")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
    [StringLength(150)]
    [Display(Name = "البريد الإلكتروني (للدخول)")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "كلمة المرور يجب ألا تقل عن 8 أحرف")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "كلمتا المرور غير متطابقتين")]
    [Display(Name = "تأكيد كلمة المرور")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "الاسم يجب أن يكون بين 3 و150 حرفاً")]
    [Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
    [StringLength(20)]
    [Display(Name = "رقم الهاتف")]
    public string? PhoneNumber { get; set; }

    public IList<string> Roles { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الحالية")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "كلمة المرور يجب ألا تقل عن 8 أحرف")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الجديدة")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "كلمتا المرور غير متطابقتين")]
    [Display(Name = "تأكيد كلمة المرور الجديدة")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
