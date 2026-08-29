using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SpaceMaker
{
    /// <summary>
    /// 磁盘占用 / 释放的核心逻辑。两种模式：
    ///  - Real   ：SetFileValidData，真正扣空间、不写数据、需管理员。
    ///  - Sparse ：稀疏文件，显示很大但不占空间。
    /// </summary>
    internal static class DiskEngine
    {
        public static string ReserveFolderFor(char drive)
        {
            return Path.Combine($"{drive}:\\", ".spacemaker");
        }

        public static Reservation Occupy(char drive, long sizeBytes, OccupyMode mode)
        {
            var id = Guid.NewGuid().ToString("N");
            var dir = ReserveFolderFor(drive);
            var path = Path.Combine(dir, $"reserve_{id}.bin");

            try
            {
                Directory.CreateDirectory(dir);
                if ((File.GetAttributes(dir) & FileAttributes.Hidden) != FileAttributes.Hidden)
                {
                    File.SetAttributes(dir, FileAttributes.Hidden);
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"无法在 {drive}:\\ 根目录创建隐藏文件夹 {dir}。" +
                    "请尝试以管理员身份运行本程序，或选择非系统盘（如 D:\\）。", ex);
            }

            IntPtr hRaw = NativeMethods.CreateFileW(
                path,
                NativeMethods.GENERIC_WRITE,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_ALWAYS,
                NativeMethods.FILE_ATTRIBUTE_NORMAL | NativeMethods.FILE_ATTRIBUTE_HIDDEN,
                IntPtr.Zero);

            if (hRaw == new IntPtr(-1) || hRaw == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                string errName = new Win32Exception(err).Message;
                throw new Win32Exception(err, $"无法创建保留文件 {path}（Win32 错误 {err}：{errName}）。" +
                    "常见原因：目标盘为系统盘且无管理员权限、磁盘只读或安全软件拦截。");
            }

            // 用显式 try/finally 管理句柄：任何一步失败都要删除已创建的文件，
            // 否则会残留占用磁盘空间且无法从 UI 释放（这正是之前 C 盘被吃满的根因）。
            var hFile = new SafeFileHandle(hRaw, true);
            bool committed = false;
            try
            {
                // 先设置文件末尾（EOF），把文件空间分配出来。
                // Real 模式下 SetFileValidData 需要 EOF >= sizeBytes，否则无法直接设置有效数据长度。
                var eof = new NativeMethods.FILE_END_OF_FILE_INFO { EndOfFile = sizeBytes };
                if (!NativeMethods.SetFileInformationByHandle(
                        hFile, NativeMethods.FileEndOfFileInfo, ref eof,
                        (uint)Marshal.SizeOf<NativeMethods.FILE_END_OF_FILE_INFO>()))
                {
                    int err = Marshal.GetLastWin32Error();
                    string errName = new Win32Exception(err).Message;
                    throw new Win32Exception(err, $"设置文件大小失败（Win32 错误 {err}：{errName}）。" +
                        "常见原因：目标盘可用空间不足、磁盘只读、或该卷不支持此操作。");
                }

                if (mode == OccupyMode.Real)
                {
                    NativeMethods.EnableManageVolumePrivilege();
                    if (!NativeMethods.SetFileValidData(hFile, sizeBytes))
                    {
                        int err = Marshal.GetLastWin32Error();
                        string errName = new Win32Exception(err).Message;
                        throw new Win32Exception(err, $"SetFileValidData 失败（Win32 错误 {err}：{errName}）。" +
                            "常见原因：未以管理员身份运行、目标盘不是 NTFS，或该卷被安全策略限制。");
                    }
                }
                else if (mode == OccupyMode.Sparse)
                {
                    uint bytesReturned;
                    if (!NativeMethods.DeviceIoControl(
                            hFile, NativeMethods.FSCTL_SET_SPARSE,
                            IntPtr.Zero, 0, IntPtr.Zero, 0,
                            out bytesReturned, IntPtr.Zero))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "设置稀疏文件失败");
                    }
                }

                committed = true;
                return new Reservation
                {
                    Id = id,
                    Drive = drive,
                    Path = path,
                    Mode = mode,
                    SizeBytes = sizeBytes,
                    CreatedAt = DateTime.Now
                };
            }
            finally
            {
                hFile.Dispose();
                // 失败（committed 仍为 false）则清理已落盘的残留文件，避免污占空间。
                if (!committed)
                {
                    try { File.Delete(path); } catch { }
                }
            }
        }

        public static void Release(Reservation r)
        {
            if (!string.IsNullOrEmpty(r.Path) && File.Exists(r.Path))
            {
                File.Delete(r.Path);
            }
        }
    }
}
