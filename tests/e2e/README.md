# اختبارات شاملة (End-to-End)

اختبارات حقيقية على التطبيق وهو يعمل. تُرسل طلبات HTTP عبر curl، وتشغّل متصفح Chrome بدون واجهة (headless). **لا تشغّلها على قاعدة بيانات الإنتاج**: هي تُنشئ مستخدمين وفرقاً وبطولات ومباريات وأخباراً.

## المتطلبات

| الأداة | الملاحظة |
|---|---|
| Git Bash | لملفات `.sh` |
| `curl` | |
| `sqlcmd` | يتصل عبر Windows Authentication، وتُستخدم بعض الاختبارات للتحقق مباشرة من قاعدة البيانات |
| Node 22+ | لملفات `.mjs` |
| Google Chrome | لملفات `.mjs` |

## التشغيل على نسخة معزولة (موصى به)

```bash
# 1) شغّل التطبيق على قاعدة بيانات اختبار منفصلة
cd S3Sport
ConnectionStrings__DefaultConnection="Server=.;Database=S3SportDb_Test;Trusted_Connection=True;TrustServerCertificate=True" \
  dotnet run --launch-profile http --urls http://localhost:5199

# 2) في نافذة أخرى
export BASE=http://localhost:5199 DB=S3SportDb_Test
export ADMIN_PW='...'   # كلمة مرور admin@s3sport.om في appsettings.Development.json (غير موجودة في المستودع)
bash tests/e2e/test_phase2.sh   # الفرق، المناطق، طلبات التسجيل، الصلاحيات
bash tests/e2e/test_follow.sh   # المتابعة والإشعارات
bash tests/e2e/test_phase3.sh   # اللاعبون، الانتقالات، أنظمة البطولات، الترتيب
bash tests/e2e/test_phase4.sh   # التسجيل المباشر، التزامن، منع التكرار، التصحيح
bash tests/e2e/test_phase5.sh   # الصفحات العامة، الأخبار، المفضلة، PWA
bash tests/e2e/test_team_matches.sh  # مسؤول الفريق: اللاعبون، المباريات الودية، التسجيل المباشر، أسماء لاعبي الفريق الضيف، الإشعارات
bash tests/e2e/test_register_team.sh # التسجيل كمستخدم أو كفريق: الحساب وطلب الفريق معاً أو لا شيء، ثم الموافقة
bash tests/e2e/test_push.sh          # إشعارات الجهاز: الاشتراك والحماية، والإرسال الفعلي إلى خدمة إشعارات وهمية تفك تشفير كل رسالة
```

كل سكربت يطبع `PASS` أو `FAIL` لكل فحص، وفي النهاية مجموع النتائج. ملفات الـ Cookies والصفحات المؤقتة تُكتب داخل هذا المجلد، وهي مستثناة في `.gitignore`.

## اختبارات المتصفح

| الأمر | ما يختبره |
|---|---|
| `node live_test.mjs <matchId> <outDir>` | متصفحان: شاشة مسؤول التسجيل والصفحة العامة. يتحقق من وصول الأهداف والأحداث عبر SignalR دون تحديث الصفحة، ومن تجاهل الضغط المكرر. |
| `node sw_check.mjs <outDir>` | تثبيت الـ Service Worker والتخزين المؤقت. للعمل بدون اتصال: أوقف الخادم ثم أعد التشغيل بالأمر `node sw_check.mjs <outDir> offline`. |
| `node prod_check.mjs <outDir>` | نسخة Release منشورة على `https://localhost:5443`: الـ Cookies الآمنة، عدم وجود مخالفات CSP، SignalR، والرسوم البيانية. يحتاج حساب مدير في تلك النسخة: `OWNER_PW`، و`OWNER_EMAIL` اختياري (الافتراضي `owner@s3sport.om`). |
| `node shots.mjs <outDir>` | لقطات شاشة للصفحات الرئيسية بمقاسات الهاتف والحاسوب، للمراجعة البصرية. |
| `node mobile_audit.mjs <outDir> <width> <pages.json>` | فحص الجوال: لقطة كاملة لكل صفحة بمقاس هاتف مع اللمس، وتقرير بخروج المحتوى عن العرض، وأهداف اللمس الأصغر من 40px، والحقول التي تسبب التكبير على آيفون، والمحتوى تحت الشريط السفلي. ملف الصفحات قائمة من الشكل `{role, path, name, click?}`، والقيمة `guest` في `role` تعني بدون تسجيل دخول. |
| `node push_browser.mjs <outDir> <fanEmail> <managerEmail> <guestMatchId>` | يفحص: تفعيل إشعارات الجهاز من صفحة الإشعارات، وعرض الـ Service Worker للإشعار، وخانات أسماء الفريق الضيف في شاشة التسجيل، وإلغاء اشتراك الجهاز عند تسجيل الخروج. |

> **ملاحظة على Windows:** اختر مجلد `outDir` بمسار قصير، مثل `C:\t\out`. في المسار الطويل تتجاوز ملفات تخزين المتصفح حد 260 حرفاً، فيفشل كل حفظ في الكاش ولا يكتمل تثبيت الـ Service Worker. `push_browser.mjs` يستخدم مجلداً قصيراً تلقائياً.
>
> **Chrome بدون واجهة (headless) لا يتصل بخدمة إشعارات Google،** لذلك `push_browser.mjs` يستبدل خطوة الاشتراك في المتصفح فقط. التشفير والإرسال الفعلي يغطيهما `test_push.sh`.

اختبارات `live_test` و`sw_check` تستخدم افتراضياً العنوان `http://localhost:5199`. أما `shots` فتستخدم `http://localhost:5107`. ويمكن تغيير العنوان في الحالتين بمتغير البيئة `BASE`.
