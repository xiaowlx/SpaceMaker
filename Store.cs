using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceMaker
{
    /// <summary>三种"占用"模式。</summary>
    internal enum OccupyMode
    {
        /// <summary>真占用：用 SetFileValidData 真正扣减可用空间，不写数据，需管理员。</summary>
        Real,
        /// <summary>稀疏文件：资源管理器显示很大，但不占实际空间。</summary>
        Sparse,
        /// <summary>纯界面：完全不碰磁盘，只在本软件里显示。</summary>
        Visual
    }

    /// <summary>一次占用的记录，持久化到本地以便随时释放。</summary>
    internal sealed class Reservation
    {
        public string Id { get; set; } = "";
        public char Drive { get; set; }
        public string Path { get; set; } = "";   // Visual 模式为空
        public OccupyMode Mode { get; set; }
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>用户设置。</summary>
    internal sealed class AppSettings
    {
        public string DefaultDrive { get; set; } = "C";
        public bool DarkTheme { get; set; } = true;
        public OccupyMode LastMode { get; set; } = OccupyMode.Real;
        public bool AutoElevate { get; set; } = false;
    }

    /// <summary>
    /// AOT 友好的 JSON 源生成上下文（避免反射裁剪导致运行时失败）。
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(List<Reservation>))]
    internal sealed partial class AppJsonContext : JsonSerializerContext
    {
    }

    /// <summary>占用记录与设置的读写（存于 %LOCALAPPDATA%\SpaceMaker）。</summary>
    internal static class Store
    {
        private static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpaceMaker");
        private static readonly string ReserveFile = Path.Combine(Dir, "reservations.json");
        private static readonly string SettingsFile = Path.Combine(Dir, "settings.json");

        static Store()
        {
            try { Directory.CreateDirectory(Dir); } catch { }
        }

        public static List<Reservation> LoadReservations()
        {
            if (!File.Exists(ReserveFile)) return new List<Reservation>();
            try
            {
                var json = File.ReadAllText(ReserveFile);
                var list = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListReservation);
                return list ?? new List<Reservation>();
            }
            catch { return new List<Reservation>(); }
        }

        public static void SaveReservations(List<Reservation> list)
        {
            try
            {
                var json = JsonSerializer.Serialize(list, AppJsonContext.Default.ListReservation);
                File.WriteAllText(ReserveFile, json);
            }
            catch { }
        }

        public static AppSettings LoadSettings()
        {
            if (!File.Exists(SettingsFile)) return new AppSettings();
            try
            {
                var json = File.ReadAllText(SettingsFile);
                var s = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);
                return s ?? new AppSettings();
            }
            catch { return new AppSettings(); }
        }

        public static void SaveSettings(AppSettings s)
        {
            try
            {
                var json = JsonSerializer.Serialize(s, AppJsonContext.Default.AppSettings);
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }
    }
}
