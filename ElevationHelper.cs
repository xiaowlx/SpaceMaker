using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SpaceMaker
{
    /// <summary>
    /// 管理「双击即管理员运行」的注册表标志。
    /// 通过 HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers
    /// 写入或删除 ~ RUNASADMIN，使系统在启动 exe 时自动弹出 UAC。
    /// </summary>
    internal static class ElevationHelper
    {
        private const string LayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        private const string RunAsAdmin = "~ RUNASADMIN";

        /// <summary>获取当前 exe 的完整路径（优先 MainModule，失败则回退当前程序路径）。</summary>
        public static string GetExePath()
        {
            try
            {
                var main = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(main) && File.Exists(main))
                    return main;
            }
            catch { }
            return Process.GetCurrentProcess().MainModule?.FileName ??
                   Path.Combine(AppContext.BaseDirectory, "SpaceMaker.exe");
        }

        /// <summary>检查当前 exe 是否已经设置了自动以管理员运行。</summary>
        public static bool IsRunAsAdminEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(LayersKey, false);
                if (key == null) return false;
                var value = key.GetValue(GetExePath()) as string;
                return value?.Contains("RUNASADMIN") == true;
            }
            catch { return false; }
        }

        /// <summary>开启或关闭「双击以管理员身份运行」。</summary>
        public static void SetRunAsAdmin(bool enable)
        {
            var path = GetExePath();
            using var key = Registry.CurrentUser.CreateSubKey(LayersKey);
            if (key == null)
                throw new InvalidOperationException("无法打开注册表项，可能没有写入权限。");

            if (enable)
                key.SetValue(path, RunAsAdmin, RegistryValueKind.String);
            else
                key.DeleteValue(path, false);
        }
    }
}
