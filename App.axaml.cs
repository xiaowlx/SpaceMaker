using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace SpaceMaker;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 切换亮/暗主题：同时切换 FluentTheme 的主题变量，并更新自定义画刷资源。
    /// 所有界面都通过 DynamicResource 引用这些画刷，因此会实时刷新。
    /// </summary>
    public static void ApplyTheme(bool dark)
    {
        AppTheme.Dark = dark;

        if (Current is App app)
        {
            app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;

            var res = app.Resources;
            res["WindowBackground"] = AppTheme.BackBrush;
            res["SidebarBackground"] = AppTheme.SidebarBrush;
            res["CardBackground"] = AppTheme.CardBrush;
            res["PanelBackground"] = AppTheme.PanelBrush;
            res["PanelHoverBackground"] = AppTheme.PanelHoverBrush;
            res["TextForeground"] = AppTheme.TextBrush;
            res["SubTextForeground"] = AppTheme.SubTextBrush;
            res["AppBorderBrush"] = AppTheme.BorderBrush;
            res["PrimaryBrush"] = AppTheme.AccentBrush;
            res["PrimaryHoverBrush"] = AppTheme.AccentHoverBrush;
            res["AccentTextBrush"] = AppTheme.AccentBrush;
            res["NavActiveBackground"] = AppTheme.NavActiveBackBrush;
            res["NavActiveTextBrush"] = AppTheme.NavActiveTextBrush;
            res["NavHoverBackground"] = AppTheme.NavHoverBackBrush;
        }
    }
}
