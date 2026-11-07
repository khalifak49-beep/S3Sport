using System.ComponentModel.DataAnnotations;

namespace S3Sport.Models;

public enum TeamStatus
{
    [Display(Name = "قيد المراجعة")] Pending = 0,
    [Display(Name = "معتمد")] Approved = 1,
    [Display(Name = "مرفوض")] Rejected = 2,
    [Display(Name = "موقوف")] Suspended = 3,
    /// <summary>An opponent outside the platform, added by a team manager for a friendly. Never listed publicly.</summary>
    [Display(Name = "فريق ضيف")] Guest = 4
}

public enum TeamMemberRole
{
    [Display(Name = "مسؤول الفريق")] Manager = 0,
    [Display(Name = "مدرب")] Coach = 1,
    [Display(Name = "إداري")] Staff = 2
}

public enum CompetitionFormat
{
    [Display(Name = "دوري نقاط")] League = 0,
    [Display(Name = "خروج المغلوب")] Knockout = 1,
    [Display(Name = "مجموعات")] Groups = 2,
    [Display(Name = "مختلط (مجموعات + خروج مغلوب)")] Mixed = 3
}

public enum CompetitionSeasonStatus
{
    [Display(Name = "مسودة")] Draft = 0,
    [Display(Name = "التسجيل مفتوح")] Registration = 1,
    [Display(Name = "جارية")] InProgress = 2,
    [Display(Name = "منتهية")] Completed = 3,
    [Display(Name = "ملغاة")] Cancelled = 4
}

public enum StageType
{
    [Display(Name = "دوري")] League = 0,
    [Display(Name = "دور المجموعات")] Group = 1,
    [Display(Name = "خروج المغلوب")] Knockout = 2
}

/// <summary>Ordered tie-break rules applied after points. Stored as a comma-separated list on CompetitionSeason.</summary>
public enum TieBreakerCriterion
{
    [Display(Name = "فارق الأهداف")] GoalDifference = 0,
    [Display(Name = "الأهداف المسجلة")] GoalsFor = 1,
    [Display(Name = "نقاط المواجهات المباشرة")] HeadToHeadPoints = 2,
    [Display(Name = "فارق أهداف المواجهات المباشرة")] HeadToHeadGoalDifference = 3,
    [Display(Name = "أهداف المواجهات المباشرة")] HeadToHeadGoalsFor = 4,
    [Display(Name = "عدد الانتصارات")] Wins = 5,
    [Display(Name = "اللعب النظيف (أقل بطاقات)")] FairPlay = 6
}

public enum MatchStatus
{
    [Display(Name = "مجدولة")] Scheduled = 0,
    [Display(Name = "مباشر")] Live = 1,
    [Display(Name = "استراحة")] HalfTime = 2,
    [Display(Name = "انتهت")] Finished = 3,
    [Display(Name = "مؤجلة")] Postponed = 4,
    [Display(Name = "ملغاة")] Cancelled = 5
}

public enum MatchEventType
{
    [Display(Name = "بداية المباراة")] MatchStarted = 0,
    [Display(Name = "هدف")] Goal = 1,
    [Display(Name = "هدف عكسي")] OwnGoal = 2,
    [Display(Name = "هدف من ركلة جزاء")] PenaltyGoal = 3,
    [Display(Name = "ركلة جزاء ضائعة")] MissedPenalty = 4,
    [Display(Name = "بطاقة صفراء")] YellowCard = 5,
    [Display(Name = "إنذار ثانٍ (طرد)")] SecondYellowCard = 6,
    [Display(Name = "بطاقة حمراء")] RedCard = 7,
    [Display(Name = "تبديل")] Substitution = 8,
    [Display(Name = "نهاية الشوط الأول")] FirstHalfEnded = 9,
    [Display(Name = "بداية الشوط الثاني")] SecondHalfStarted = 10,
    [Display(Name = "نهاية المباراة")] MatchEnded = 11
}

public enum MatchOfficialRole
{
    [Display(Name = "حكم الساحة")] Referee = 0,
    [Display(Name = "مساعد أول")] AssistantReferee1 = 1,
    [Display(Name = "مساعد ثانٍ")] AssistantReferee2 = 2,
    [Display(Name = "الحكم الرابع")] FourthOfficial = 3,
    [Display(Name = "مراقب المباراة")] Commissioner = 4
}

public enum EventRevisionAction
{
    [Display(Name = "تعديل")] Updated = 0,
    [Display(Name = "إلغاء")] Cancelled = 1,
    [Display(Name = "استعادة")] Restored = 2
}

public enum NewsCategory
{
    [Display(Name = "عام")] General = 0,
    [Display(Name = "أخبار الفرق")] TeamNews = 1,
    [Display(Name = "أخبار البطولات")] CompetitionNews = 2,
    [Display(Name = "نتائج المباريات")] MatchResult = 3,
    [Display(Name = "انتقالات")] Transfer = 4,
    [Display(Name = "صور")] Gallery = 5,
    [Display(Name = "فيديو")] Video = 6
}

public enum MediaType
{
    [Display(Name = "صورة")] Image = 0,
    [Display(Name = "فيديو")] Video = 1
}

public enum FollowTargetType
{
    [Display(Name = "فريق")] Team = 0,
    [Display(Name = "لاعب")] Player = 1,
    [Display(Name = "بطولة")] Competition = 2,
    [Display(Name = "مباراة")] Match = 3
}

public enum NotificationType
{
    [Display(Name = "بداية مباراة")] MatchStarted = 0,
    [Display(Name = "هدف")] Goal = 1,
    [Display(Name = "نهاية الشوط الأول")] HalfTime = 2,
    [Display(Name = "نهاية مباراة")] MatchEnded = 3,
    [Display(Name = "تغيير موعد")] MatchRescheduled = 4,
    [Display(Name = "خبر جديد")] NewsPublished = 5,
    [Display(Name = "طلب فريق")] TeamApplication = 6,
    [Display(Name = "عام")] General = 7,
    [Display(Name = "بداية الشوط الثاني")] SecondHalfStarted = 8,
    [Display(Name = "بطاقة")] Card = 9,
    [Display(Name = "ركلة جزاء")] Penalty = 10,
    [Display(Name = "تبديل")] Substitution = 11,
    [Display(Name = "مباراة جديدة")] MatchScheduled = 12,
    [Display(Name = "عرض مباراة ودية")] FriendlyOffer = 13,
    [Display(Name = "رد على عرض مباراة ودية")] FriendlyReply = 14
}

/// <summary>Where a team that looks for a friendly wants to play.</summary>
public enum FriendlyVenuePreference
{
    [Display(Name = "في ملعبنا")] Home = 0,
    [Display(Name = "في ملعب الخصم")] Away = 1,
    [Display(Name = "أي ملعب")] Either = 2
}

public enum FriendlyRequestStatus
{
    [Display(Name = "مفتوح")] Open = 0,
    [Display(Name = "تم الاتفاق")] Matched = 1,
    [Display(Name = "مغلق")] Closed = 2
}

public enum FriendlyOfferStatus
{
    [Display(Name = "بانتظار الرد")] Pending = 0,
    [Display(Name = "مقبول")] Accepted = 1,
    [Display(Name = "مرفوض")] Declined = 2,
    [Display(Name = "مسحوب")] Withdrawn = 3
}

public enum SponsorScope
{
    [Display(Name = "كل السلطنة")] Country = 0,
    [Display(Name = "محافظة")] Governorate = 1,
    [Display(Name = "ولاية")] Wilayat = 2,
    [Display(Name = "بطولة")] Competition = 3
}
