using UnityEngine;

public enum DevicePlatformFamily
{
    Desktop,
    Mobile,
    Console,
    Web,
    Unknown
}

public static class DevicePlatformService
{
    public static DevicePlatformFamily CurrentFamily
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

    public static bool IsDesktopLike()
    {
        return CurrentFamily == DevicePlatformFamily.Desktop;
    }

    public static bool ShouldUseVirtualJoystick()
    {
        return !IsDesktopLike();
    }
}
