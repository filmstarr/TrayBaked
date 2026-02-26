using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace TrayBaked;

static class NotificationHelper
{
    private const string Aumid = "TrayBaked.Notifications";

    /// <summary>
    /// Registers the app's AUMID in the current user's registry and associates
    /// this process with it so Windows shows the correct icon in the notification
    /// attribution line (tiny icon next to the app name at the top of each toast).
    /// Safe to call on every startup.
    /// </summary>
    public static void EnsureRegistered()
    {
        try
        {
            var iconPath = AppIconHelper.GetOrSaveIconPng();

            using var key = Registry.CurrentUser.CreateSubKey(
                $@"SOFTWARE\Classes\AppUserModelId\{Aumid}");
            key?.SetValue("DisplayName", "TrayBaked");
            key?.SetValue("IconUri", iconPath);
        }
        catch { /* non-fatal */ }

        // Associate the running process with the AUMID so Windows can look up
        // the registered icon for the notification header attribution line.
        try { SetCurrentProcessExplicitAppUserModelID(Aumid); } catch { }
    }

    public static ToastNotification CreateToast(
        string body,
        params (string Label, string Arg)[] buttons)
    {
        var iconUri = new Uri(AppIconHelper.GetOrSaveIconPng()).AbsoluteUri;

        var btns = string.Concat(buttons.Select(b =>
            $"<action content=\"{Xml(b.Label)}\" arguments=\"{Xml(b.Arg)}\" activationType=\"foreground\"/>"));

        var xml = $"""
            <toast>
              <visual>
                <binding template="ToastGeneric">
                  <image placement="appLogoOverride" src="{iconUri}" hint-crop="none"/>
                  <text>TrayBaked</text>
                  <text>{Xml(body)}</text>
                </binding>
              </visual>
              <actions>
                {btns}
              </actions>
            </toast>
            """;

        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return new ToastNotification(doc);
    }

    public static void Show(ToastNotification toast)
    {
        ToastNotificationManager.CreateToastNotifier(Aumid).Show(toast);
    }

    private static string Xml(string s) =>
        s.Replace("&", "&amp;")
         .Replace("<", "&lt;")
         .Replace(">", "&gt;")
         .Replace("\"", "&quot;");

    [DllImport("shell32.dll", PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string AppID);
}
