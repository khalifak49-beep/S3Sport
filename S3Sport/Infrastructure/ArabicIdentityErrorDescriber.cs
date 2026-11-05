using Microsoft.AspNetCore.Identity;

namespace S3Sport.Infrastructure;

public class ArabicIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = $"البريد الإلكتروني {email} مسجل مسبقاً." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = $"اسم المستخدم {userName} مستخدم مسبقاً." };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = "البريد الإلكتروني غير صالح." };
    public override IdentityError InvalidUserName(string? userName) => new() { Code = nameof(InvalidUserName), Description = "اسم المستخدم غير صالح." };
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"كلمة المرور يجب ألا تقل عن {length} أحرف." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "كلمة المرور يجب أن تحتوي على رقم واحد على الأقل." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "كلمة المرور يجب أن تحتوي على حرف صغير." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "كلمة المرور يجب أن تحتوي على حرف كبير." };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "كلمة المرور يجب أن تحتوي على رمز." };
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "كلمة المرور الحالية غير صحيحة." };
    public override IdentityError UserAlreadyInRole(string role) => new() { Code = nameof(UserAlreadyInRole), Description = "المستخدم يملك هذا الدور مسبقاً." };
    public override IdentityError UserNotInRole(string role) => new() { Code = nameof(UserNotInRole), Description = "المستخدم لا يملك هذا الدور." };
    public override IdentityError ConcurrencyFailure() => new() { Code = nameof(ConcurrencyFailure), Description = "تم تعديل البيانات من مستخدم آخر، يرجى إعادة المحاولة." };
    public override IdentityError DefaultError() => new() { Code = nameof(DefaultError), Description = "حدث خطأ غير متوقع." };
}
