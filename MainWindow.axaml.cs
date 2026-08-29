using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Platform;
using SpaceMaker.Models;

namespace SpaceMaker
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly List<Reservation> _reservations;
        private readonly IUpdateSource _updater = new DisabledUpdateSource();
        private readonly bool _isAdmin;
        private bool _sidebarCollapsed;

        // 界面显示的版本号直接读程序集版本（随 SpaceMaker.csproj 的 <Version> 变化），
        // 发新版时不必再改界面里的硬编码文本。
        private static string DisplayVersion =>
            Assembly.GetExecutingAssembly().GetName().Version is { } v
                ? $"{v.Major}.{v.Minor}.{v.Build}"
                : "未知";

        public MainWindow()
        {
            _settings = Store.LoadSettings();
            _reservations = Store.LoadReservations();
            _isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);

            InitializeComponent();
            AppVersionText.Text = $"空间魔术师  v{DisplayVersion}";
            InitializeComboBoxes();
            App.ApplyTheme(_settings.DarkTheme);
            RefreshAdminStatus();
            RefreshDrives();
            RecoverOrphans();
            RefreshReservations();
            RefreshFreeSpace();
            SyncAutoElevateRegistry();

            // 通过命令行 --grant-privilege / --revoke-privilege 启动时（由对应按钮以管理员身份拉起），
            // 在窗口打开后自动执行授予或撤销。
            var args2 = Environment.GetCommandLineArgs();
            bool grantRequested = args2.Any(a => a.Equals("--grant-privilege", StringComparison.OrdinalIgnoreCase));
            bool revokeRequested = args2.Any(a => a.Equals("--revoke-privilege", StringComparison.OrdinalIgnoreCase));
            this.Opened += (_, _) =>
            {
                FitWindowToScreen();
                if (grantRequested)
                    DoGrantPrivilege();
                else if (revokeRequested)
                    DoRevokePrivilege();
            };
            // 显式设置窗口图标，确保标题栏/任务栏显示，并与资源管理器中的 exe 预览一致。
            try
            {
                using var iconStream = AssetLoader.Open(new Uri("avares://SpaceMaker/Assets/icon.ico"));
                Icon = new WindowIcon(iconStream);
            }
            catch { }

            SelectPage(HomePanel);
        }

        private void SyncAutoElevateRegistry()
        {
            try
            {
                var actual = ElevationHelper.IsRunAsAdminEnabled();
                if (actual != _settings.AutoElevate)
                    ElevationHelper.SetRunAsAdmin(_settings.AutoElevate);
            }
            catch { }
        }

        private void FitWindowToScreen()
        {
            var screen = Screens.Primary;
            if (screen == null) return;

            double scale = screen.Scaling;
            double workW = screen.WorkingArea.Width / scale;
            double workH = screen.WorkingArea.Height / scale;

            // 默认占屏幕工作区的 70%，但不超过 1024x700、不小于最小尺寸
            double w = Math.Clamp(workW * 0.70, MinWidth, Math.Min(1024, workW - 48));
            double h = Math.Clamp(workH * 0.70, MinHeight, Math.Min(700, workH - 48));

            Width = w;
            Height = h;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private void InitializeComboBoxes()
        {
            ModeBox.ItemsSource = new[] { "真占用（需管理员）", "稀疏文件（显大不占空间）" };
            ModeBox.SelectedIndex = (int)_settings.LastMode;

            SetModeBox.ItemsSource = new[] { "真占用（需管理员）", "稀疏文件（显大不占空间）" };
            SetModeBox.SelectedIndex = (int)_settings.LastMode;

            SetDarkCheck.IsChecked = _settings.DarkTheme;
            SetAutoElevateCheck.IsChecked = _settings.AutoElevate;

            // 真占用提示卡初始可见性：仅当默认模式为"真占用"(索引 0) 时显示。
            RealModeHint.IsVisible = ModeBox.SelectedIndex == 0;
        }

        private void ModeBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // 索引 0 = 真占用：显示特权提示与自动提权按钮；其它模式隐藏。
            RealModeHint.IsVisible = ModeBox.SelectedIndex == 0;
        }

        private void RefreshAdminStatus()
        {
            if (_isAdmin)
            {
                AdminStatusText.Text = "已以管理员身份运行，可使用全部模式。";
                AdminStatusText.Foreground = new SolidColorBrush(AppTheme.Text);
                BtnElevate.IsVisible = false;
            }
            else
            {
                var hint = _settings.AutoElevate
                    ? "下次启动将自动请求管理员权限。"
                    : "请点击右侧按钮以管理员身份运行。";
                AdminStatusText.Text = $"未以管理员身份运行：真占用模式不可用。{hint}";
                AdminStatusText.Foreground = new SolidColorBrush(AppTheme.SubText);
                BtnElevate.IsVisible = true;
            }
        }

        private void RefreshDrives()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => d.Name)
                .ToList();

            DriveBox.ItemsSource = drives;
            SetDriveBox.ItemsSource = drives;

            string def = _settings.DefaultDrive + ":\\";
            DriveBox.SelectedItem = drives.Contains(def) ? def : drives.FirstOrDefault();
            SetDriveBox.SelectedItem = drives.Contains(def) ? def : drives.FirstOrDefault();
        }

        private void RefreshFreeSpace()
        {
            if (DriveBox.SelectedItem is not string d)
            {
                FreeSpaceText.Text = "";
                return;
            }

            if (NativeMethods.GetDiskFreeSpaceEx(d, out ulong free, out ulong total, out _))
                FreeSpaceText.Text = $"可用空间：{FormatSize((long)free)} / 共 {FormatSize((long)total)}";
            else
                FreeSpaceText.Text = "无法读取磁盘信息";
        }

        /// <summary>
        /// 启动时扫描各固定盘的 .spacemaker 目录，把残留的 reserve_*.bin 找回并加入释放列表。
        /// 这些文件可能是之前占用中途报错、或程序被直接关闭时留下的（未被记录进 reservations.json）。
        /// </summary>
        private void RecoverOrphans()
        {
            try
            {
                foreach (var di in DriveInfo.GetDrives())
                {
                    if (!di.IsReady || di.DriveType != DriveType.Fixed) continue;
                    char drive = di.Name[0];
                    string dir = DiskEngine.ReserveFolderFor(drive);
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.GetFiles(dir, "reserve_*.bin"))
                    {
                        if (_reservations.Any(r => r.Path == f)) continue;
                        var fi = new FileInfo(f);
                        _reservations.Add(new Reservation
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Drive = drive,
                            Path = f,
                            Mode = OccupyMode.Real,
                            SizeBytes = fi.Length,
                            CreatedAt = fi.CreationTime
                        });
                    }
                }
                Store.SaveReservations(_reservations);
            }
            catch { }
        }

        private void RefreshReservations()
        {
            var items = _reservations.Select(r => new ReservationItem
            {
                Reservation = r,
                DisplayLine1 = $"{r.Drive}:\\   {FormatSize(r.SizeBytes)}",
                DisplayLine2 = $"[{ModeText(r.Mode)}]   {r.CreatedAt:g}"
            }).ToList();

            ReservationList.ItemsSource = items;
            EmptyReservationText.IsVisible = items.Count == 0;
            ReservationList.IsVisible = items.Count > 0;
        }

        // ---------------------------------------------------------------- Navigation
        private void SelectPage(Panel page)
        {
            HomePanel.IsVisible = page == HomePanel;
            SettingsPanel.IsVisible = page == SettingsPanel;
            AboutPanel.IsVisible = page == AboutPanel;

            NavHome.Classes.Set("Active", page == HomePanel);
            NavSettings.Classes.Set("Active", page == SettingsPanel);
            NavAbout.Classes.Set("Active", page == AboutPanel);
        }

        private void NavHome_Click(object? sender, PointerPressedEventArgs e) => SelectPage(HomePanel);
        private void NavSettings_Click(object? sender, PointerPressedEventArgs e) => SelectPage(SettingsPanel);
        private void NavAbout_Click(object? sender, PointerPressedEventArgs e) => SelectPage(AboutPanel);

        // ---------------------------------------------------------------- Home Events
        private void DriveBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            RefreshFreeSpace();
        }

        private void Quick1_Click(object? sender, RoutedEventArgs e) => SizeBox.Value = 1;
        private void Quick10_Click(object? sender, RoutedEventArgs e) => SizeBox.Value = 10;
        private void Quick50_Click(object? sender, RoutedEventArgs e) => SizeBox.Value = 50;
        private void Quick100_Click(object? sender, RoutedEventArgs e) => SizeBox.Value = 100;

        private void BtnOccupy_Click(object? sender, RoutedEventArgs e)
        {
            if (DriveBox.SelectedItem is not string driveStr || string.IsNullOrEmpty(driveStr)) return;
            char drive = driveStr[0];
            double gb = (double)(SizeBox.Value ?? 0);
            long bytes = (long)(gb * 1024 * 1024 * 1024);
            if (bytes <= 0)
            {
                ShowMessage("提示", "大小必须大于 0。");
                return;
            }

            var mode = (OccupyMode)(ModeBox.SelectedIndex);
            if (mode == OccupyMode.Real && !_isAdmin)
            {
                ShowMessage("需要管理员", "真占用模式需要管理员权限。请点击“以管理员重启”，或以管理员身份运行本程序。");
                return;
            }

            // 占用前检查可用空间（真占用与稀疏文件都适用，避免后续 API 报磁盘满）。
            if (NativeMethods.GetDiskFreeSpaceEx($"{drive}:\\", out ulong freeBytes, out _, out _))
            {
                if ((ulong)bytes > freeBytes)
                {
                    ShowMessage("空间不足", $"目标盘 {drive}:\\ 可用空间仅 {FormatSize((long)freeBytes)}，小于请求的 {FormatSize(bytes)}。请减小占用大小或清理磁盘。");
                    return;
                }
            }

            try
            {
                var r = DiskEngine.Occupy(drive, bytes, mode);
                _reservations.Add(r);
                Store.SaveReservations(_reservations);
                RefreshReservations();
                RefreshFreeSpace();
                ShowMessage("完成", $"已占用 {FormatSize(bytes)}（{ModeText(mode)}）。");
            }
            catch (Exception ex)
            {
                ShowMessage("错误", "占用失败：" + ex.Message);
            }
        }

        private void BtnReleaseOne_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReservationItem item)
            {
                try { DiskEngine.Release(item.Reservation); } catch { }
                _reservations.Remove(item.Reservation);
                Store.SaveReservations(_reservations);
                RefreshReservations();
                RefreshFreeSpace();
            }
        }

        private void BtnReleaseAll_Click(object? sender, RoutedEventArgs e)
        {
            if (_reservations.Count == 0) return;
            // Simple confirmation
            foreach (var r in _reservations.ToList())
            {
                try { DiskEngine.Release(r); } catch { }
            }
            _reservations.Clear();
            Store.SaveReservations(_reservations);
            RefreshReservations();
            RefreshFreeSpace();
        }

        private void BtnElevate_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule?.FileName ?? "SpaceMaker.exe",
                    Verb = "runas",
                    UseShellExecute = true
                };
                Process.Start(psi);
                Close();
            }
            catch
            {
                ShowMessage("提示", "已取消提权，或以管理员重启失败。");
            }
        }

        /// <summary>
        /// 真占用提示卡里的"自动提权"按钮：把 SeManageVolumePrivilege 真正授予当前账户。
        /// 修改 LSA 策略需要管理员权限：当前不是管理员时，以管理员身份重启本程序并带上
        /// --grant-privilege 参数，由提权后的实例完成授权。
        /// </summary>
        private void BtnGrantPrivilege_Click(object? sender, RoutedEventArgs e)
        {
            if (!_isAdmin)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Process.GetCurrentProcess().MainModule?.FileName ?? "SpaceMaker.exe",
                        Arguments = "--grant-privilege",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    // 已拉起管理员实例去完成授权，关闭当前非管理员窗口，避免两个窗口并存。
                    Close();
                }
                catch
                {
                    ShowMessage("提示", "已取消，或未获得管理员权限，无法写入本地安全策略。");
                }
                return;
            }

            DoGrantPrivilege();
        }

        /// <summary>
        /// 执行实际的特权授予（调用方需已是管理员）。
        /// 授予后当前会话不会立即生效，必须注销并重新登录。
        /// </summary>
        private void DoGrantPrivilege()
        {
            try
            {
                NativeMethods.GrantManageVolumePrivilegeToCurrentUser();
                ShowMessage("已授予「执行卷维护任务」特权",
                    "已为当前账户添加 SeManageVolumePrivilege（执行卷维护任务）特权。\n\n" +
                    "该特权写入了本地安全策略，但【当前登录会话】尚未生效。\n" +
                    "请【注销并重新登录】或【重启电脑】，之后无论是否以管理员运行本程序，真占用模式都可直接使用，不再报 1314。");
            }
            catch (Exception ex)
            {
                ShowMessage("授权失败", "无法授予特权：" + ex.Message + "\n\n如仍失败，可手动在 secpol.msc 中添加，或用内置 Administrator 账户运行。");
            }
        }

        /// <summary>
        /// 真占用提示卡里的"恢复默认（撤销特权）"按钮：把 SeManageVolumePrivilege 从当前账户移除。
        /// 同样需要管理员权限（LSA 写），不足时以 runas 重启并带 --revoke-privilege 参数。
        /// </summary>
        private void BtnRevokePrivilege_Click(object? sender, RoutedEventArgs e)
        {
            if (!_isAdmin)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Process.GetCurrentProcess().MainModule?.FileName ?? "SpaceMaker.exe",
                        Arguments = "--revoke-privilege",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    // 已拉起管理员实例去完成撤销，关闭当前非管理员窗口。
                    Close();
                }
                catch
                {
                    ShowMessage("提示", "已取消，或未获得管理员权限，无法写入本地安全策略。");
                }
                return;
            }

            DoRevokePrivilege();
        }

        /// <summary>
        /// 执行实际的特权撤销（调用方需已是管理员）。撤销后同样需注销并重新登录才彻底生效。
        /// </summary>
        private void DoRevokePrivilege()
        {
            try
            {
                NativeMethods.RevokeManageVolumePrivilegeFromCurrentUser();
                ShowMessage("已恢复系统默认",
                    "已从当前账户移除 SeManageVolumePrivilege（执行卷维护任务）特权。\n\n" +
                    "该账户回到系统默认状态（不再固定持有此特权）。\n" +
                    "若你之前已授予，当前仍生效的会话需【注销并重新登录】后才彻底剥除；之后真占用模式若无管理员+该特权，会再次报 1314。");
            }
            catch (Exception ex)
            {
                ShowMessage("撤销失败", "无法撤销特权：" + ex.Message);
            }
        }

        // ---------------------------------------------------------------- Settings Events (全部自动保存)
        private void SetDriveBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => SaveSettings();
        private void SetModeBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => SaveSettings();
        private void SetAutoElevateCheck_Changed(object? sender, RoutedEventArgs e) => SaveSettings();

        private void SetDarkCheck_Changed(object? sender, RoutedEventArgs e)
        {
            _settings.DarkTheme = SetDarkCheck.IsChecked == true;
            App.ApplyTheme(_settings.DarkTheme);
            Store.SaveSettings(_settings);

            SettingsSavedText.IsVisible = true;
            Task.Delay(1500).ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() => SettingsSavedText.IsVisible = false));
        }

        private void SaveSettings()
        {
            if (SetDriveBox.SelectedItem is string d)
                _settings.DefaultDrive = d[0].ToString();
            _settings.LastMode = (OccupyMode)(SetModeBox.SelectedIndex);
            _settings.AutoElevate = SetAutoElevateCheck.IsChecked == true;
            Store.SaveSettings(_settings);

            // 同步主页的默认模式下拉框
            ModeBox.SelectedIndex = SetModeBox.SelectedIndex;

            // 同步注册表「自动以管理员身份运行」标志
            SyncAutoElevateRegistry();
            RefreshAdminStatus();

            SettingsSavedText.IsVisible = true;
            Task.Delay(1500).ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() => SettingsSavedText.IsVisible = false));
        }

        // ---------------------------------------------------------------- About Events
        private async void BtnCheckUpdate_Click(object? sender, RoutedEventArgs e)
        {
            UpdateStatusText.Text = "正在检查更新…";
            try
            {
                var info = await _updater.CheckAsync();
                UpdateStatusText.Text = info.HasUpdate
                    ? $"发现新版本 {info.Version}：{info.Notes}"
                    : $"已是最新。{info.Notes}";
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = "检查失败：" + ex.Message;
            }
        }

        // ---------------------------------------------------------------- Helpers
        private static string ModeText(OccupyMode m) => m switch
        {
            OccupyMode.Real => "真占用",
            OccupyMode.Sparse => "稀疏文件",
            _ => "未知"
        };

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double v = bytes;
            int i = 0;
            while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {units[i]}";
        }

        private void ShowMessage(string title, string message)
        {
            // 用主窗口内浮层显示消息（非新弹窗），自动淡入淡出，可点空白处关闭。
            _ = ShowOverlayAsync(title, message);
        }

        /// <summary>把侧栏在展开(220)与收起(0)之间切换，带平滑动画。</summary>
        private async void BtnToggleSidebar_Click(object? sender, RoutedEventArgs e)
        {
            _sidebarCollapsed = !_sidebarCollapsed;
            double from = Sidebar.Width;
            double to = _sidebarCollapsed ? 0 : 220;
            // 折叠/展开图标切换：用矢量 Path 避免字体字形的视觉偏移。
            ToggleSidebarIconPath.Data = StreamGeometry.Parse(
                _sidebarCollapsed ? "M 2 5 L 14 5 M 2 9 L 14 9 M 2 13 L 14 13" : "M 10 3 L 5 8 L 10 13");
            await TweenDoubleAsync(v => Sidebar.Width = v, from, to, 220);
        }

        /// <summary>点浮层暗色背景（空白处）时关闭浮层；点卡片/按钮不会被吞掉。</summary>
        private async void OverlayLayer_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == OverlayLayer)
            {
                await DismissOverlayAsync();
                e.Handled = true;
            }
        }

        private async Task ShowOverlayAsync(string title, string message)
        {
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Foreground = AppTheme.TextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            var messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = AppTheme.TextBrush,
                TextWrapping = TextWrapping.Wrap
            };
            var okButton = new Button
            {
                Content = "确定",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                MinWidth = 120
            };
            okButton.Click += async (_, _) => await DismissOverlayAsync();

            var card = new Border
            {
                Background = AppTheme.CardBrush,
                BorderBrush = AppTheme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(AppTheme.CornerRadius),
                Padding = new Thickness(24),
                MinWidth = 360,
                MaxWidth = 480,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new StackPanel { Spacing = 14, Children = { titleBlock, messageBlock, okButton } }
            };

            OverlayLayer.Children.Clear();
            OverlayLayer.Children.Add(card);
            OverlayLayer.Opacity = 0;
            OverlayLayer.IsVisible = true;
            await TweenDoubleAsync(v => OverlayLayer.Opacity = v, 0, 1, 180);
        }

        private async Task DismissOverlayAsync()
        {
            if (!OverlayLayer.IsVisible) return;
            double from = OverlayLayer.Opacity;
            await TweenDoubleAsync(v => OverlayLayer.Opacity = v, from, 0, 160);
            OverlayLayer.IsVisible = false;
            OverlayLayer.Children.Clear();
        }

        /// <summary>简单的 UI 线程平滑过渡（smoothstep 缓动），避免引入 Avalonia Animation 命名空间复杂度。</summary>
        private static async Task TweenDoubleAsync(Action<double> setter, double from, double to, int durationMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < durationMs)
            {
                var t = Math.Clamp(sw.ElapsedMilliseconds / (double)durationMs, 0, 1);
                var eased = t * t * (3 - 2 * t);
                setter(from + (to - from) * eased);
                await Task.Delay(16);
            }
            setter(to);
        }
    }
}
