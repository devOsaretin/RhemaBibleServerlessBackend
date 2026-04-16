using System.Globalization;
using System.Text;
using RhemaBibleAppServerless.Domain.Enums;

namespace RhemaBibleAppServerless.Shared.Helpers;

public static class EmailTemplates
{
    private static string Layout(string title, string preheader, string contentHtml)
    {
        var sb = new StringBuilder();
        sb.Append("""
<!doctype html>
<html>
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1" />
    <title>
""");
        sb.Append(Escape(title));
        sb.Append("""
</title>
  </head>
  <body style="margin:0;padding:0;background:rgb(254,252,248);font-family:ui-sans-serif,system-ui,-apple-system,Segoe UI,Roboto,Helvetica,Arial;">
    <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">
""");
        sb.Append(Escape(preheader));
        sb.Append("""
    </div>
    <div style="padding:28px 12px;">
      <div style="max-width:640px;margin:0 auto;background:#FFFFFF;border:1px solid rgba(124,152,133,0.2);border-radius:14px;overflow:hidden;">
        <div style="padding:18px 22px;background:linear-gradient(135deg,rgba(79,116,87,0.1),rgba(124,152,133,0.05));border-bottom:1px solid rgba(124,152,133,0.2);">
          <div style="font-size:15px;color:rgb(107,114,128);margin-bottom:6px;">Rhema Bible</div>
          <div style="font-size:20px;line-height:1.25;color:rgb(31,41,55);font-weight:700;">
""");
        sb.Append(Escape(title));
        sb.Append("""
          </div>
        </div>
        <div style="padding:22px;">
""");
        sb.Append(contentHtml);
        sb.Append("""
          <div style="margin-top:18px;padding-top:16px;border-top:1px solid rgb(229,231,235);color:rgb(107,114,128);font-size:13px;line-height:1.5;">
            <div style="margin-bottom:8px;">May the Lord bless you and keep you.</div>
            <div style="color:#9CA3AF;">If you need help, email <a href="mailto:support@rhemabible.app" style="color:#4F7457;text-decoration:none;font-weight:600;">support@rhemabible.app</a>.</div>
          </div>
        </div>
      </div>
      <div style="max-width:640px;margin:10px auto 0;color:#9CA3AF;font-size:12px;text-align:center;">
        ©
""");
        sb.Append(DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture));
        sb.Append("""
 Rhema Bible
      </div>
    </div>
  </body>
</html>
""");
        return sb.ToString();
    }

    public static string VerificationCode(string code, int lifetimeMinutes)
    {
        var body = $"""
<div style="color:rgb(31,41,55);font-size:15px;line-height:1.65;">
  <div style="margin-bottom:10px;">Your verification code is below.</div>
  <div style="display:inline-block;padding:14px 16px;border-radius:12px;background:rgba(124,152,133,0.1);border:1px solid rgba(124,152,133,0.2);font-size:22px;letter-spacing:2px;font-weight:800;color:#4F7457;">
    {Escape(code)}
  </div>
  <div style="margin-top:12px;color:rgb(107,114,128);">This code expires in {lifetimeMinutes} minutes.</div>
</div>
""";
        return Layout("Your verification code", "Your Rhema Bible verification code", body);
    }

    public static string SubscriptionPurchased(SubscriptionType type, DateTime? expiresAtUtc)
    {
        var plan = ResolvePlanLabel(type, expiresAtUtc);

        var expires = expiresAtUtc.HasValue
            ? $"Renews/valid until: <b>{expiresAtUtc.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC</b>."
            : "Your premium access is active.";

        var body = $"""
<div style="color:rgb(31,41,55);font-size:15px;line-height:1.65;">
  <div style="margin-bottom:10px;">Thank you for subscribing.</div>
  <div style="padding:14px 16px;border-radius:12px;background:rgba(212,175,55,0.1);border:1px solid rgba(212,175,55,0.2);color:rgb(31,41,55);">
    <div style="font-weight:700;margin-bottom:4px;">Plan</div>
    <div style="color:#4F7457;font-weight:800;">{Escape(plan)}</div>
    <div style="margin-top:10px;color:rgb(107,114,128);">{expires}</div>
  </div>
  <div style="margin-top:14px;color:rgb(107,114,128);">
    You now have full access to premium AI features and resources.
  </div>
</div>
""";

        return Layout("Subscription activated", "Your subscription is active", body);
    }

    public static string SubscriptionExpired()
    {
        var body = """
<div style="color:rgb(31,41,55);font-size:15px;line-height:1.65;">
  <div style="margin-bottom:10px;">Your premium subscription has expired.</div>
  <div style="padding:14px 16px;border-radius:12px;background:rgba(220,38,38,0.08);border:1px solid rgb(229,231,235);">
    <div style="font-weight:700;color:rgb(31,41,55);margin-bottom:6px;">What this means</div>
    <div style="color:rgb(107,114,128);">Your account has moved back to the Free plan.</div>
  </div>
  <div style="margin-top:14px;color:rgb(107,114,128);">
    If you’d like to continue with premium features, you can resubscribe anytime in the app.
  </div>
</div>
""";

        return Layout("Subscription expired", "Your premium subscription has expired", body);
    }

    public static string PasswordChanged(DateTime changedAtUtc)
    {
        var when = changedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var body = $"""
<div style="color:rgb(31,41,55);font-size:15px;line-height:1.65;">
  <div style="margin-bottom:10px;">Your Rhema Bible account password was just changed.</div>
  <div style="padding:14px 16px;border-radius:12px;background:rgba(79,116,87,0.08);border:1px solid rgba(124,152,133,0.2);">
    <div style="font-weight:700;color:rgb(31,41,55);margin-bottom:6px;">When</div>
    <div style="color:rgb(107,114,128);">{when} UTC</div>
  </div>
  <div style="margin-top:14px;color:rgb(107,114,128);">
    If you did not make this change, contact support immediately so we can help secure your account.
  </div>
</div>
""";

        return Layout("Password updated", "Your Rhema Bible password was changed", body);
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string ResolvePlanLabel(SubscriptionType type, DateTime? expiresAtUtc)
    {
        if (type == SubscriptionType.PremiumYearly) return "Premium (Yearly)";
        if (type == SubscriptionType.PremiumMonthly) return "Premium (Monthly)";
        if (type == SubscriptionType.Premium) return "Premium";

        if (expiresAtUtc.HasValue)
        {
            var days = (expiresAtUtc.Value.ToUniversalTime() - DateTime.UtcNow).TotalDays;
            if (days >= 300) return "Premium (Yearly)";
            if (days >= 20) return "Premium (Monthly)";
        }

        return "Premium";
    }
}

