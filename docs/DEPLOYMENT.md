# دليل نشر S3Sport على Windows Server / IIS

هذا الدليل يغطي النشر الأول والتحديثات اللاحقة. جُرِّبت الخطوات الأساسية محلياً على نسخة Release منشورة تعمل بوضع Production:
- إنشاء قاعدة بيانات فارغة من `deploy/migrate.sql`.
- تشغيل النسخة المنشورة بوضع Production.
- التحقق من: HTTPS، ترويسات الأمان، `/health`، SignalR، وإنشاء المدير الأول من متغيرات البيئة.

## 1) المتطلبات على الخادم

| المكوّن | ملاحظات |
|---|---|
| Windows Server 2019 أو أحدث، و IIS 10 | |
| **ASP.NET Core 8 Hosting Bundle** | من صفحة تحميل .NET 8. أعد تشغيل IIS بعد التثبيت: `iisreset` |
| ميزة **WebSocket Protocol** في IIS | عبر: Server Manager ← Add Roles and Features ← Web Server ← Application Development ← WebSocket Protocol. بدونها تعمل النتائج المباشرة بطريقة أبطأ (Long Polling). |
| SQL Server 2019 أو 2022 | يمكن أن يكون على نفس الخادم أو خادم آخر |
| شهادة SSL للنطاق | مثل Let's Encrypt عبر أداة win-acme، أو شهادة تجارية |

## 2) قاعدة البيانات

1. أنشئ قاعدة بيانات فارغة باسم `S3SportDb`.
2. نفّذ السكربت `deploy/migrate.sql`. هو **idempotent**: آمن للتشغيل أكثر من مرة، ولا يطبّق إلا الـ migrations الناقصة.
   ```powershell
   sqlcmd -S . -E -d S3SportDb -I -b -i deploy\migrate.sql
   ```
3. أنشئ دخولاً لهوية تجمّع التطبيقات (App Pool)، وامنحه صلاحيات القراءة والكتابة فقط:
   ```sql
   CREATE LOGIN [IIS APPPOOL\S3Sport] FROM WINDOWS;
   USE S3SportDb;
   CREATE USER [IIS APPPOOL\S3Sport] FOR LOGIN [IIS APPPOOL\S3Sport];
   ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\S3Sport];
   ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\S3Sport];
   ```
   إذا كان SQL Server على خادم آخر، استخدم دخول SQL باسم مستخدم وكلمة مرور في سلسلة الاتصال.

> **لتوليد السكربت بعد إضافة migrations جديدة** (على جهاز التطوير):
> `dotnet ef migrations script --idempotent -o deploy/migrate.sql`

## 3) النشر

على جهاز التطوير:
```powershell
cd S3Sport
dotnet publish -c Release -o ..\publish
```
- انسخ مجلد `publish` إلى الخادم، مثلاً `D:\Sites\S3Sport`.
- ملف `appsettings.Development.json` **لا يُنشر** لأنه مستبعد في ملف المشروع (يحتوي كلمة مرور التطوير).

## 4) إعداد IIS

1. **Application Pool جديد** باسم `S3Sport`:
   - .NET CLR version: **No Managed Code**
   - Managed pipeline: Integrated
   - Start mode: AlwaysRunning (اختياري، لتسريع أول طلب)
   - Idle Time-out: `0` (حتى لا تتوقف الاتصالات المباشرة)
2. **Site جديد** يشير إلى `D:\Sites\S3Sport` ويستخدم الـ App Pool السابق.
3. **صلاحيات الكتابة** لهوية `IIS APPPOOL\S3Sport` على المجلدات التالية فقط:
   - `wwwroot\uploads`: شعارات الفرق، صور اللاعبين، صور الأخبار.
   - `App_Data`: السجلات `logs` ومفاتيح التشفير `keys`. يُنشأ تلقائياً عند أول تشغيل.
   ```powershell
   icacls D:\Sites\S3Sport\wwwroot\uploads /grant "IIS APPPOOL\S3Sport:(OI)(CI)M"
   New-Item -ItemType Directory -Force D:\Sites\S3Sport\App_Data | Out-Null
   icacls D:\Sites\S3Sport\App_Data /grant "IIS APPPOOL\S3Sport:(OI)(CI)M"
   ```
4. ملف `web.config` المرفق يتولى ما يلي:
   - تشغيل التطبيق داخل IIS (in-process) بوضع `Production`.
   - تفعيل WebSockets.
   - حد رفع الملفات 32 ميجابايت.
   - إخفاء مجلد `App_Data` وإزالة ترويسة الخادم.

## 5) الإعدادات السرية (لا تضعها في ملفات المشروع)

الأفضل تعريفها كمتغيرات بيئة للـ App Pool في IIS Manager: الموقع ← Configuration Editor ← `system.webServer/aspNetCore` ← `environmentVariables`. ويمكن بدلاً من ذلك تعديل `appsettings.Production.json` على الخادم فقط.

| المتغير | القيمة |
|---|---|
| `ConnectionStrings__DefaultConnection` | `Server=.;Database=S3SportDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True` |
| `SeedAdmin__Email` / `SeedAdmin__Password` | **لأول تشغيل فقط**: ينشئ حساب مدير النظام الأعلى. احذفهما بعد الدخول وتغيير كلمة المرور. |
| `DataProtection__KeysPath` | اختياري. الافتراضي `App_Data\keys`. عند تشغيل أكثر من خادم يجب أن يكون مجلداً مشتركاً. |
| `WebPush__PublicKey` / `WebPush__PrivateKey` | اختياري. مفاتيح VAPID لإشعارات الجهاز. إذا لم تُحدَّد تُولَّد تلقائياً عند أول تشغيل وتُحفظ في `App_Data\vapid.json`. لا تغيّرها بعد التشغيل، لأن ذلك يُبطل اشتراكات الأجهزة. |
| `WebPush__Subject` | اختياري. بريد للتواصل تراه خدمات الإشعارات، مثل `mailto:admin@your-domain`. |

## 6) HTTPS

1. أضف Binding من نوع **https** على المنفذ 443 بالشهادة.
2. احتفظ بـ Binding للـ http على المنفذ 80. التطبيق يحوّل تلقائياً إلى HTTPS ويرسل ترويسة HSTS.
3. في بيئة Production تكون ملفات تعريف الارتباط (Cookies) **Secure** دائماً، لذلك لا يمكن تسجيل الدخول عبر http.
4. مثال بأداة win-acme لـ Let's Encrypt:
   ```powershell
   wacs.exe --target iis --siteid <SiteId> --installation iis
   ```
   تجدد الأداة الشهادة تلقائياً.

## 7) التحقق بعد النشر

| الفحص | المتوقع |
|---|---|
| `https://your-domain/health` | `Healthy` (يتحقق من الاتصال بقاعدة البيانات) |
| الصفحة الرئيسية | تظهر بالعربية، وتحتوي ترويسات `Content-Security-Policy` و`Strict-Transport-Security` |
| تسجيل الدخول بالمدير الأول | ينجح. ثم احذف متغيري `SeedAdmin` وغيّر كلمة المرور من «حسابي». |
| فتح مباراة من شاشة التسجيل وصفحتها العامة في متصفحين | الهدف يظهر فوراً دون تحديث الصفحة (SignalR) |
| تثبيت الموقع على الهاتف | يظهر خيار «إضافة إلى الشاشة الرئيسية» (PWA). يحتاج HTTPS بشهادة صالحة. |
| إشعارات الجهاز | من «الإشعارات» اضغط «تفعيل على هذا الجهاز»، ثم سجّل حدثاً في مباراة يتابعها هذا الحساب وأغلق الموقع: يصل الإشعار للجهاز. على آيفون يعمل فقط بعد إضافة الموقع للشاشة الرئيسية (iOS 16.4 أو أحدث). الخادم يحتاج اتصالاً صادراً بالإنترنت على المنفذ 443. |
| السجلات | `App_Data\logs\s3sport-YYYYMMDD.log`، ملف لكل يوم، ويُحتفظ بآخر 30 يوماً |

## 8) التحديثات

1. ولّد `migrate.sql` الجديد ونفّذه على قاعدة البيانات. هو آمن لأنه لا يطبّق إلا ما ينقص.
2. أوقف الموقع مؤقتاً بوضع ملف `app_offline.htm` في جذر الموقع.
3. انسخ ملفات النشر الجديدة. **لا تحذف** المجلدين `wwwroot\uploads` و`App_Data`.
4. احذف `app_offline.htm`.

مفاتيح `App_Data\keys` تبقى كما هي، فلا يُسجَّل خروج المستخدمين عند التحديث.

## 9) النسخ الاحتياطي

احتفظ بنسخ احتياطية منتظمة من ثلاثة أشياء:
- **قاعدة البيانات:** عبر SQL Agent، أو `BACKUP DATABASE S3SportDb TO DISK=...`.
- **المجلد `wwwroot\uploads`:** الصور المرفوعة.
- **المجلد `App_Data\keys`:** بدونه تبطل جلسات الدخول والنماذج المفتوحة عند الاستعادة.
- **الملف `App_Data\vapid.json`:** بدونه يتوقف وصول إشعارات الأجهزة حتى يعيد كل مستخدم فتح الموقع.

## 10) استكشاف الأخطاء

| العَرَض | السبب المحتمل |
|---|---|
| الخطأ `500.19` أو `500.30` عند البدء | الـ Hosting Bundle غير مثبت، أو لم يُنفّذ `iisreset` بعده |
| يعمل التطبيق لكن تظهر رسالة خطأ في الاتصال بقاعدة البيانات | سلسلة الاتصال غير صحيحة، أو صلاحيات `IIS APPPOOL\S3Sport` على قاعدة البيانات ناقصة |
| فشل رفع الصور | صلاحية الكتابة على `wwwroot\uploads` ناقصة |
| النتائج المباشرة تتأخر حوالي 30 ثانية | ميزة WebSocket Protocol غير مفعّلة في IIS |
| يُسجَّل خروج المستخدمين بعد كل إعادة تشغيل | صلاحية الكتابة على `App_Data\keys` ناقصة |

لتشخيص مشاكل بدء التشغيل: فعّل `stdoutLogEnabled="true"` مؤقتاً في `web.config` وراجع `App_Data\logs\stdout_*.log`.
