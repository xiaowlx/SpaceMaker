#nullable disable

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace SpaceMaker
{
    /// <summary>
    /// 所有需要的 Windows API 声明，以及启用 "管理卷" 特权的辅助方法。
    /// </summary>
    internal static class NativeMethods
    {
        // ---- 文件 / 磁盘相关常量 ----
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        // 注意：CREATE/OPEN/TRUNCATE 的常量值是 Win32 固定值，不要写错。
        // OPEN_ALWAYS = 4，不是 3（3 是 OPEN_EXISTING）。
        public const uint OPEN_ALWAYS = 4;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public const uint FILE_ATTRIBUTE_HIDDEN = 0x2;
        public const uint FSCTL_SET_SPARSE = 0x000900C4;
        public const int FileEndOfFileInfo = 6;

        // ---- 特权相关常量 ----
        public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        public const uint TOKEN_QUERY = 0x0008;
        public const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        // 返回 IntPtr 而不是 SafeFileHandle：避免 SafeHandle 构造过程覆盖 GetLastWin32Error，
        // 确保在创建失败时拿到准确的 Win32 错误码。
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// 把文件的"有效数据长度"直接设为指定大小，使 NTFS 分配簇但不写入任何字节。
        /// 需要 SE_MANAGE_VOLUME_NAME 特权（管理员）。
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetFileValidData(SafeFileHandle hFile, long ValidDataLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetFileInformationByHandle(
            SafeFileHandle hFile,
            int FileInformationClass,
            ref FILE_END_OF_FILE_INFO lpFileInformation,
            uint dwBufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceEx(
            string lpDirectoryName,
            out ulong lpFreeBytesAvailableToCaller,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        // ---- 特权提升 ----
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenProcessToken(
            IntPtr ProcessHandle,
            uint DesiredAccess,
            out IntPtr TokenHandle);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LookupPrivilegeValue(
            string lpSystemName,
            string lpName,
            out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
            [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            uint BufferLength,
            IntPtr PreviousState,
            IntPtr ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PrivilegeCheck(
            IntPtr ClientToken,
            ref PRIVILEGE_SET RequiredPrivileges,
            [MarshalAs(UnmanagedType.Bool)] out bool pfResult);

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PRIVILEGE_SET
        {
            public uint PrivilegeCount;
            public uint Control;
            public LUID_AND_ATTRIBUTES Privilege;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FILE_END_OF_FILE_INFO
        {
            public long EndOfFile;
        }

        // ---- LSA 策略：用于把用户权限（特权）直接写入本地安全策略 ----
        public const uint POLICY_ALL_ACCESS = 0x000F0FFF;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LSA_UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LSA_OBJECT_ATTRIBUTES
        {
            public int Length;
            public IntPtr RootDirectory;
            public IntPtr ObjectName;
            public uint Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        [DllImport("advapi32.dll")]
        public static extern uint LsaOpenPolicy(
            IntPtr SystemName,
            ref LSA_OBJECT_ATTRIBUTES ObjectAttributes,
            uint DesiredAccess,
            out IntPtr PolicyHandle);

        [DllImport("advapi32.dll")]
        public static extern uint LsaAddAccountRights(
            IntPtr PolicyHandle,
            IntPtr AccountSid,
            LSA_UNICODE_STRING[] UserRights,
            uint CountOfRights);

        [DllImport("advapi32.dll")]
        public static extern uint LsaRemoveAccountRights(
            IntPtr PolicyHandle,
            IntPtr AccountSid,
            [MarshalAs(UnmanagedType.Bool)] bool AllRights,
            LSA_UNICODE_STRING[] UserRights,
            uint CountOfRights);

        [DllImport("advapi32.dll")]
        public static extern uint LsaClose(IntPtr PolicyHandle);

        [DllImport("advapi32.dll")]
        public static extern uint LsaNtStatusToWin32Error(uint Status);

        /// <summary>
        /// 查询当前进程令牌中 SeManageVolumePrivilege 是否已启用。
        /// 返回 true 表示特权存在且已启用；false 表示不存在、未启用或查询失败。
        /// </summary>
        public static bool IsManageVolumePrivilegeEnabled()
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_QUERY, out IntPtr token))
                return false;

            try
            {
                if (!LookupPrivilegeValue(null, "SeManageVolumePrivilege", out LUID luid))
                    return false;

                var ps = new PRIVILEGE_SET
                {
                    PrivilegeCount = 1,
                    Control = 0,
                    Privilege = new LUID_AND_ATTRIBUTES
                    {
                        Luid = luid,
                        Attributes = SE_PRIVILEGE_ENABLED
                    }
                };

                if (!PrivilegeCheck(token, ref ps, out bool result))
                    return false;

                return result;
            }
            finally
            {
                CloseHandle(token);
            }
        }

        /// <summary>
        /// 在当前进程令牌里启用 SeManageVolumePrivilege，供 SetFileValidData 使用。
        /// 即使以管理员运行，该特权默认也是"禁用"状态，必须显式开启。
        /// 开启失败会抛出 Win32Exception，说明具体原因。
        /// </summary>
        public static void EnableManageVolumePrivilege()
        {
            if (!OpenProcessToken(
                    Process.GetCurrentProcess().Handle,
                    TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY,
                    out IntPtr token))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法打开进程令牌（OpenProcessToken 失败）");
            }

            try
            {
                if (!LookupPrivilegeValue(null, "SeManageVolumePrivilege", out LUID luid))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "查找 SeManageVolumePrivilege 失败");
                }

                var tp = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                };

                // BufferLength 是 PreviousState 缓冲区大小；PreviousState 为 NULL 时按 MSDN 填 0。
                if (!AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "AdjustTokenPrivileges 调用失败");
                }

                // AdjustTokenPrivileges 返回 true 不代表所有特权都成功启用。
                // ERROR_NOT_ALL_ASSIGNED(1300) 表示当前令牌根本没有这个特权（未以管理员运行）。
                int err = Marshal.GetLastWin32Error();
                if (err == 1300)
                {
                    throw new Win32Exception(err, "当前进程没有 SeManageVolumePrivilege 特权（请以管理员身份运行本程序）");
                }

                // 再次校验：部分系统上 AdjustTokenPrivileges 不报错，但特权并未真正生效，
                // 导致后续 SetFileValidData 直接 1314。用 PrivilegeCheck 确认是否真的启用。
                if (!IsManageVolumePrivilegeEnabled())
                {
                    throw new InvalidOperationException(
                        "已尝试启用 SeManageVolumePrivilege，但校验发现该特权仍未生效。\n\n" +
                        "常见原因：当前 Windows 账户未被授予「执行卷维护任务」权限，或组策略/UAC 过滤掉了该特权。\n\n" +
                        "解决方法（任选其一）：\n" +
                        "1. 使用系统内置的 Administrator 账户运行本程序；\n" +
                        "2. 按 Win+R 输入 secpol.msc → 本地策略 → 用户权限分配 → 双击「执行卷维护任务」→ 添加当前账户 → 注销并重新登录。");
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }

        /// <summary>
        /// 通过 LSA 策略，把 SeManageVolumePrivilege（执行卷维护任务）特权直接授予当前登录账户。
        /// 这是"自动提权"的治本做法：一次性写入本地安全策略，之后该账户所有会话都拥有此特权。
        /// 注意：本方法修改的是机器级 LSA 策略，需要管理员权限（LsaOpenPolicy 需要 POLICY_ALL_ACCESS）。
        /// 授予后当前进程令牌不会立即拥有该特权，需注销并重新登录（或重启）后才生效。
        /// </summary>
        public static void GrantManageVolumePrivilegeToCurrentUser()
        {
            var sid = WindowsIdentity.GetCurrent().User;
            if (sid == null)
                throw new InvalidOperationException("无法获取当前用户的 SID，无法授予特权。");

            byte[] sidBytes = new byte[sid.BinaryLength];
            sid.GetBinaryForm(sidBytes, 0);

            IntPtr sidPtr = Marshal.AllocHGlobal(sidBytes.Length);
            Marshal.Copy(sidBytes, 0, sidPtr, sidBytes.Length);

            IntPtr policyHandle = IntPtr.Zero;
            var attrs = new LSA_OBJECT_ATTRIBUTES { Length = Marshal.SizeOf<LSA_OBJECT_ATTRIBUTES>() };

            uint status = LsaOpenPolicy(IntPtr.Zero, ref attrs, POLICY_ALL_ACCESS, out policyHandle);
            if (status != 0)
                throw new Win32Exception((int)LsaNtStatusToWin32Error(status),
                    "打开本地安全策略失败（LsaOpenPolicy）。请确认以管理员身份运行。");

            try
            {
                const string priv = "SeManageVolumePrivilege";
                IntPtr buf = Marshal.StringToHGlobalUni(priv);
                var us = new LSA_UNICODE_STRING
                {
                    Length = (ushort)(priv.Length * 2),
                    MaximumLength = (ushort)((priv.Length + 1) * 2),
                    Buffer = buf
                };

                try
                {
                    LSA_UNICODE_STRING[] rights = { us };
                    status = LsaAddAccountRights(policyHandle, sidPtr, rights, 1);
                    if (status != 0)
                        throw new Win32Exception((int)LsaNtStatusToWin32Error(status),
                            "为当前账户添加「执行卷维护任务」特权失败（LsaAddAccountRights）。");
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            finally
            {
                LsaClose(policyHandle);
                Marshal.FreeHGlobal(sidPtr);
            }
        }

        /// <summary>
        /// 撤销：把 SeManageVolumePrivilege（执行卷维护任务）从当前账户移除，恢复到系统默认状态
        /// （即不再把该特权固定写入账户）。同样需要管理员权限（LSA 写）。
        /// 注意：移除后当前会话仍可能持有该特权，需注销并重新登录才彻底生效。
        /// </summary>
        public static void RevokeManageVolumePrivilegeFromCurrentUser()
        {
            var sid = WindowsIdentity.GetCurrent().User;
            if (sid == null)
                throw new InvalidOperationException("无法获取当前用户的 SID，无法撤销特权。");

            byte[] sidBytes = new byte[sid.BinaryLength];
            sid.GetBinaryForm(sidBytes, 0);

            IntPtr sidPtr = Marshal.AllocHGlobal(sidBytes.Length);
            Marshal.Copy(sidBytes, 0, sidPtr, sidBytes.Length);

            IntPtr policyHandle = IntPtr.Zero;
            var attrs = new LSA_OBJECT_ATTRIBUTES { Length = Marshal.SizeOf<LSA_OBJECT_ATTRIBUTES>() };

            uint status = LsaOpenPolicy(IntPtr.Zero, ref attrs, POLICY_ALL_ACCESS, out policyHandle);
            if (status != 0)
                throw new Win32Exception((int)LsaNtStatusToWin32Error(status),
                    "打开本地安全策略失败（LsaOpenPolicy）。请确认以管理员身份运行。");

            try
            {
                const string priv = "SeManageVolumePrivilege";
                IntPtr buf = Marshal.StringToHGlobalUni(priv);
                var us = new LSA_UNICODE_STRING
                {
                    Length = (ushort)(priv.Length * 2),
                    MaximumLength = (ushort)((priv.Length + 1) * 2),
                    Buffer = buf
                };

                try
                {
                    LSA_UNICODE_STRING[] rights = { us };
                    // AllRights=false：仅移除指定的特权，保留账户其它已有权限。
                    status = LsaRemoveAccountRights(policyHandle, sidPtr, false, rights, 1);
                    if (status != 0)
                        throw new Win32Exception((int)LsaNtStatusToWin32Error(status),
                            "移除「执行卷维护任务」特权失败（LsaRemoveAccountRights）。");
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            finally
            {
                LsaClose(policyHandle);
                Marshal.FreeHGlobal(sidPtr);
            }
        }
    }
}
