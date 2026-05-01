using UnityEngine;

public enum DevicePlatformFamily
{
    Desktop,
    Mobile,
    Console,
    Web,
    Unknown
}

internal sealed class RuntimePlatformService : IPlatformService
{
    public DevicePlatformFamily CurrentFamily
    {
        get
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return DevicePlatformFamily.Desktop;

                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
                    return DevicePlatformFamily.Mobile;

                case RuntimePlatform.PS4:
                case RuntimePlatform.PS5:
                case RuntimePlatform.XboxOne:
                case RuntimePlatform.GameCoreXboxOne:
                case RuntimePlatform.GameCoreXboxSeries:
                case RuntimePlatform.Switch:
                    return DevicePlatformFamily.Console;

                case RuntimePlatform.WebGLPlayer:
                    return DevicePlatformFamily.Web;

                default:
                    return SystemInfo.deviceType == DeviceType.Handheld
                        ? DevicePlatformFamily.Mobile
                        : DevicePlatformFamily.Unknown;
            }
        }
    }

    public bool IsDesktopLike()
    {
        return CurrentFamily == DevicePlatformFamily.Desktop;
    }

    public bool ShouldUseVirtualJoystick()
    {
        return !IsDesktopLike();
    }
}

public static class DevicePlatformService
{
    private static readonly IPlatformService Runtime = new RuntimePlatformService();

    public static DevicePlatformFamily CurrentFamily => Runtime.CurrentFamily;

    public static bool IsDesktopLike()
    {
        return Runtime.IsDesktopLike();
    }

    public static bool ShouldUseVirtualJoystick()
    {
        return Runtime.ShouldUseVirtualJoystick();
    }
}
