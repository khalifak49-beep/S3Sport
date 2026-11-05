using S3Sport.Models;
using Microsoft.EntityFrameworkCore;

namespace S3Sport.Data;

/// <summary>
/// Reference data shipped with migrations (fixed ids): Oman's 11 governorates and their wilayats,
/// and Football as the first sport. Areas/villages are added from the admin panel
/// (a small demo set is inserted at runtime by <see cref="DbInitializer"/> in Development).
/// </summary>
public static class SeedData
{
    private static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public const int FootballSportId = 1;

    public static void Apply(ModelBuilder builder)
    {
        var governorates = new (int Id, string Ar, string En, (string Ar, string En)[] Wilayats)[]
        {
            (1, "مسقط", "Muscat", [
                ("مسقط", "Muscat"), ("مطرح", "Muttrah"), ("بوشر", "Bawshar"),
                ("السيب", "As Seeb"), ("العامرات", "Al Amarat"), ("قريات", "Qurayyat")]),
            (2, "ظفار", "Dhofar", [
                ("صلالة", "Salalah"), ("طاقة", "Taqah"), ("مرباط", "Mirbat"), ("رخيوت", "Rakhyut"),
                ("ثمريت", "Thumrait"), ("ضلكوت", "Dhalkut"), ("المزيونة", "Al Mazyunah"), ("مقشن", "Muqshin"),
                ("شليم وجزر الحلانيات", "Shalim and the Hallaniyat Islands"), ("سدح", "Sadah")]),
            (3, "مسندم", "Musandam", [
                ("خصب", "Khasab"), ("دبا", "Dibba"), ("بخا", "Bukha"), ("مدحاء", "Madha")]),
            (4, "البريمي", "Al Buraimi", [
                ("البريمي", "Al Buraimi"), ("محضة", "Mahdah"), ("السنينة", "As Sunaynah")]),
            (5, "الداخلية", "Ad Dakhiliyah", [
                ("نزوى", "Nizwa"), ("بهلاء", "Bahla"), ("منح", "Manah"), ("الحمراء", "Al Hamra"),
                ("أدم", "Adam"), ("إزكي", "Izki"), ("سمائل", "Samail"), ("بدبد", "Bidbid")]),
            (6, "شمال الباطنة", "North Al Batinah", [
                ("صحار", "Sohar"), ("شناص", "Shinas"), ("لوى", "Liwa"), ("صحم", "Saham"),
                ("الخابورة", "Al Khaburah"), ("السويق", "As Suwayq")]),
            (7, "جنوب الباطنة", "South Al Batinah", [
                ("الرستاق", "Ar Rustaq"), ("العوابي", "Al Awabi"), ("نخل", "Nakhal"),
                ("وادي المعاول", "Wadi Al Maawil"), ("بركاء", "Barka"), ("المصنعة", "Al Musannah")]),
            (8, "جنوب الشرقية", "South Ash Sharqiyah", [
                ("صور", "Sur"), ("الكامل والوافي", "Al Kamil Wal Wafi"), ("جعلان بني بو حسن", "Jaalan Bani Bu Hassan"),
                ("جعلان بني بو علي", "Jaalan Bani Bu Ali"), ("مصيرة", "Masirah")]),
            (9, "شمال الشرقية", "North Ash Sharqiyah", [
                ("إبراء", "Ibra"), ("المضيبي", "Al Mudaybi"), ("بدية", "Bidiyah"), ("القابل", "Al Qabil"),
                ("وادي بني خالد", "Wadi Bani Khalid"), ("دماء والطائيين", "Dima Wa At Taiyyin")]),
            (10, "الظاهرة", "Ad Dhahirah", [
                ("عبري", "Ibri"), ("ينقل", "Yanqul"), ("ضنك", "Dhank")]),
            (11, "الوسطى", "Al Wusta", [
                ("هيماء", "Haima"), ("محوت", "Mahut"), ("الدقم", "Duqm"), ("الجازر", "Al Jazir")])
        };

        var wilayatId = 0;
        foreach (var g in governorates)
        {
            builder.Entity<Governorate>().HasData(new Governorate
            {
                Id = g.Id, NameAr = g.Ar, NameEn = g.En, SortOrder = g.Id, IsActive = true, CreatedAt = SeedDate
            });

            var order = 0;
            foreach (var w in g.Wilayats)
            {
                builder.Entity<Wilayat>().HasData(new Wilayat
                {
                    Id = ++wilayatId, GovernorateId = g.Id, NameAr = w.Ar, NameEn = w.En,
                    SortOrder = ++order, IsActive = true, CreatedAt = SeedDate
                });
            }
        }

        builder.Entity<Sport>().HasData(new Sport
        {
            Id = FootballSportId, NameAr = "كرة القدم", NameEn = "Football", Code = "football",
            IconClass = "fa-solid fa-futbol", PlayersOnField = 11, PeriodsCount = 2, PeriodDurationMinutes = 45,
            PointsForWin = 3, PointsForDraw = 1, PointsForLoss = 0, IsActive = true, SortOrder = 1, CreatedAt = SeedDate
        });

        builder.Entity<SportPosition>().HasData(
            new SportPosition { Id = 1, SportId = FootballSportId, Code = "GK", NameAr = "حارس مرمى", NameEn = "Goalkeeper", SortOrder = 1, CreatedAt = SeedDate },
            new SportPosition { Id = 2, SportId = FootballSportId, Code = "DF", NameAr = "مدافع", NameEn = "Defender", SortOrder = 2, CreatedAt = SeedDate },
            new SportPosition { Id = 3, SportId = FootballSportId, Code = "MF", NameAr = "لاعب وسط", NameEn = "Midfielder", SortOrder = 3, CreatedAt = SeedDate },
            new SportPosition { Id = 4, SportId = FootballSportId, Code = "FW", NameAr = "مهاجم", NameEn = "Forward", SortOrder = 4, CreatedAt = SeedDate });
    }
}
