using Avalonia.Media;

namespace SpaceMaker
{
    /// <summary>Win11 风格主题配色，集中管理便于统一换肤。</summary>
    internal static class AppTheme
    {
        public static bool Dark { get; set; } = true;

        public static Color Back => Dark ? Color.FromRgb(31, 31, 31) : Color.FromRgb(243, 243, 243);
        public static Color Sidebar => Dark ? Color.FromRgb(37, 37, 37) : Color.FromRgb(249, 249, 249);
        public static Color Card => Dark ? Color.FromRgb(45, 45, 45) : Color.FromRgb(255, 255, 255);
        public static Color Panel => Dark ? Color.FromRgb(55, 55, 55) : Color.FromRgb(240, 240, 240);
        public static Color PanelHover => Dark ? Color.FromRgb(74, 74, 74) : Color.FromRgb(225, 225, 225);
        public static Color Text => Dark ? Color.FromRgb(245, 245, 245) : Color.FromRgb(26, 26, 26);
        public static Color SubText => Dark ? Color.FromRgb(166, 166, 166) : Color.FromRgb(96, 96, 96);
        public static Color Accent => Dark ? Color.FromRgb(10, 132, 255) : Color.FromRgb(10, 102, 194);
        public static Color AccentHover => Dark ? Color.FromRgb(64, 160, 255) : Color.FromRgb(14, 123, 229);
        public static Color Border => Dark ? Color.FromRgb(58, 58, 58) : Color.FromRgb(229, 229, 229);

        public static Color NavActiveBack => Dark ? Color.FromRgb(42, 53, 72) : Color.FromRgb(231, 241, 251);
        public static Color NavActiveText => Dark ? Color.FromRgb(90, 180, 255) : Color.FromRgb(10, 102, 194);
        public static Color NavHoverBack => Dark ? Color.FromRgb(50, 50, 50) : Color.FromRgb(235, 235, 235);

        public static IBrush BackBrush => new SolidColorBrush(Back);
        public static IBrush SidebarBrush => new SolidColorBrush(Sidebar);
        public static IBrush CardBrush => new SolidColorBrush(Card);
        public static IBrush PanelBrush => new SolidColorBrush(Panel);
        public static IBrush PanelHoverBrush => new SolidColorBrush(PanelHover);
        public static IBrush TextBrush => new SolidColorBrush(Text);
        public static IBrush SubTextBrush => new SolidColorBrush(SubText);
        public static IBrush AccentBrush => new SolidColorBrush(Accent);
        public static IBrush AccentHoverBrush => new SolidColorBrush(AccentHover);
        public static IBrush BorderBrush => new SolidColorBrush(Border);
        public static IBrush NavActiveBackBrush => new SolidColorBrush(NavActiveBack);
        public static IBrush NavActiveTextBrush => new SolidColorBrush(NavActiveText);
        public static IBrush NavHoverBackBrush => new SolidColorBrush(NavHoverBack);

        public const int CornerRadius = 10;
    }
}
