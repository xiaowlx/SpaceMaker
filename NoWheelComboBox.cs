using Avalonia.Controls;
using Avalonia.Input;

namespace SpaceMaker
{
    /// <summary>
    /// 滚轮不切换选中项的下拉框：仅允许鼠标点选。
    /// 与「在外部 AddHandler 并标记 Handled」不同，这里不吞掉滚轮事件——
    /// 事件会继续冒泡到外层 ScrollViewer，因此鼠标悬停在框上时页面仍可用滚轮滚动。
    /// </summary>
    public class NoWheelComboBox : ComboBox
    {
        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            // 刻意不调用 base：禁用 SelectingItemsControl 的「滚轮切换选中项」逻辑。
            // 同时不设置 e.Handled，让事件照常向上冒泡，交给外层 ScrollViewer 滚动页面。
        }
    }
}
