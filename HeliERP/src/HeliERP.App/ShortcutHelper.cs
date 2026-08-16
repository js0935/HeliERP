// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Windows.Forms;

namespace HeliERP.App;

/// <summary>
/// 全域快捷鍵：F2 新增、F3 修改、F4 刪除、F5 重整、Ctrl+F 搜尋／查詢。
/// 各表單在建構式結尾呼叫 <see cref="Enable"/> 掛接。
/// </summary>
public static class ShortcutHelper
{
    public static void Enable(Form form, Action? onAdd = null, Action? onEdit = null,
        Action? onDelete = null, Action? onSearch = null, Action? onReload = null)
    {
        form.KeyPreview = true;
        form.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F2 && onAdd is not null) { onAdd(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F3 && onEdit is not null) { onEdit(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F4 && onDelete is not null) { onDelete(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F5 && onReload is not null) { onReload(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.F && onSearch is not null) { onSearch(); e.SuppressKeyPress = true; }
        };
    }
}
