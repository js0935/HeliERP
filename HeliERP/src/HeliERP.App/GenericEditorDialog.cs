// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Globalization;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 泛型單列編輯對話框：依「欄位定義」動態產生「欄位標籤 + 輸入控制項」。
/// 有欄位定義的表（TableFields）依定義產生（含下拉選單、必填、隱藏系統欄）；
/// 未定義的表依欄名與資料型別自動判斷。
/// 新增／編輯共用，確定後將輸入值寫回資料列（呼叫端再儲存）。
/// </summary>
public sealed class GenericEditorDialog : Form
{
    private readonly TableInfo _table;
    private readonly DataRow _row;
    private readonly bool _isNew;
    private readonly List<(ColumnInfo Col, Control Ctrl)> _fields = new();

    private GenericEditorDialog(TableInfo table, DataRow row)
    {
        _table = table;
        _row = row;
        _isNew = row.RowState == DataRowState.Detached;

        var defs = TableFields.Get(table.Name);

        Text = _isNew ? $"新增 - {table.Name}" : $"編輯 - {table.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UiTheme.Background;
        Font = UiTheme.Font(10F);
        AutoScroll = true;

        // ── 版面順序：定義順序在前；未列出的欄接尾（有定義的表自動隱藏系統欄） ──
        var planned = BuildFieldOrder(table, defs);

        // 第一階段：建立全部控制項（供下拉帶入事件參照）
        var built = new List<(ColumnInfo Col, Control Ctrl, FieldDef? Def)>();
        var byName = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        foreach (var (col, def) in planned)
        {
            var ctrl = MakeControl(col, def);
            built.Add((col, ctrl, def));
            byName[col.Name] = ctrl;
        }

        // 第二階段：下拉選擇時自動帶入來源表其他欄位
        foreach (var (col, ctrl, def) in built)
        {
            if (def?.Kind == FieldKind.Lookup && ctrl is ComboBox cb && def.LookupCopy is { Count: > 0 })
            {
                var map = def.LookupCopy;
                cb.SelectedValueChanged += (s, e) => ApplyLookupCopy(cb, map, byName);
            }
        }

        int y = 16;
        bool hasWide = false;
        foreach (var (col, ctrl, def) in built)
        {
            bool wide = ctrl is TextBox { Multiline: true };
            if (wide) hasWide = true;

            bool readOnly = !_isNew && col.IsPrimaryKey;
            if (ctrl is TextBox tb && readOnly)
            {
                tb.ReadOnly = true;
                UiTheme.StyleTextBox(tb, true);
            }

            bool required = def?.Required ?? col.NotNull;
            var lbl = new Label
            {
                Text = (def?.Label ?? col.Name) + (required ? " *" : ""),
                Font = UiTheme.Font(9.5F),
                ForeColor = readOnly ? UiTheme.TextSub : UiTheme.TextMain,
                AutoSize = true,
                Location = new Point(24, y + 6),
            };
            ctrl.Location = new Point(wide ? 24 : 170, y);
            if (ctrl is TextBox { Multiline: true })
                ctrl.Width = 540;
            else if (ctrl is TextBox or NumericUpDown or ComboBox)
                ctrl.Width = 300;

            _fields.Add((col, ctrl));
            Controls.Add(lbl);
            Controls.Add(ctrl);
            y += wide ? 92 : 38;
        }

        // ── 按鈕 ──
        int w = hasWide ? 640 : 560;
        var btnOk = new ModernButton { Text = "確定", Size = new Size(96, 40), Location = new Point(w - 226, y + 10), IsPrimary = true };
        var btnCancel = new ModernButton { Text = "取消", Size = new Size(80, 40), Location = new Point(w - 118, y + 10), IsPrimary = false, DrawShadow = false };
        btnOk.Click += (s, e) =>
        {
            string? err = Validate();
            if (err is not null)
            {
                MessageBox.Show(this, err, "請修正", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            WriteBack();
            DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        ClientSize = new Size(w, Math.Min(y + 72, 720));
        UiTheme.ClampToScreen(this);
    }

    /// <summary>顯示新增/編輯對話框，確定後值已寫入資料列。回傳是否按確定。</summary>
    public static bool ShowDialog(IWin32Window? owner, string tableName, DataRow row)
    {
        var table = SchemaReader.GetTable(tableName)
            ?? throw new InvalidOperationException($"找不到資料表「{tableName}」");
        using var dlg = new GenericEditorDialog(table, row);
        return dlg.ShowDialog(owner) == DialogResult.OK;
    }

    // ── 欄位順序規劃 ──
    private static List<(ColumnInfo, FieldDef?)> BuildFieldOrder(TableInfo table, IReadOnlyList<FieldDef>? defs)
    {
        var result = new List<(ColumnInfo, FieldDef?)>();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (defs is not null)
        {
            foreach (var def in defs)
            {
                if (def.Kind == FieldKind.Hidden)
                    continue;
                var col = table.Columns.FirstOrDefault(c => c.Name == def.Name);
                if (col is null)
                    continue;   // 定義欄在資料庫已不存在（結構異動）→ 略過
                result.Add((col, def));
                taken.Add(col.Name);
            }
            foreach (var col in table.Columns)
            {
                if (taken.Contains(col.Name) || TableFields.IsAutoHidden(col.Name))
                    continue;
                result.Add((col, null));
            }
        }
        else
        {
            foreach (var col in table.Columns)
                result.Add((col, null));
        }
        return result;
    }

    // ── 控制項產生 ──
    private Control MakeControl(ColumnInfo col, FieldDef? def)
    {
        var clr = col.ClrType;
        var value = _row.RowState == DataRowState.Detached ? null : _row[col.Name];
        var kind = def?.Kind ?? FieldKind.Auto;

        // 下拉選單
        if (kind == FieldKind.Lookup && def is not null)
        {
            try
            {
                var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UiTheme.Font(10F) };
                var extraCols = def.LookupCopy is { Count: > 0 }
                    ? ", " + string.Join(", ", def.LookupCopy.Values.Distinct().Select(c => $"[{c}]"))
                    : "";
                var src = DbManager.QueryTable(
                    $"SELECT DISTINCT [{def.LookupValue}] AS [值], [{def.LookupDisplay}] AS [顯示]{extraCols} FROM [{def.LookupTable}] ORDER BY [值]");
                cb.DataSource = src;
                cb.DisplayMember = "顯示";
                cb.ValueMember = "值";
                UiTheme.StyleComboBox(cb);
                SetComboValue(cb, value);
                return cb;
            }
            catch (Exception)
            {
                // 來源表或欄位不存在時退回文字框
            }
        }

        // 明確型別
        switch (kind)
        {
            case FieldKind.Bool:
                return MakeBool(col, value);
            case FieldKind.Date:
                return MakeDate(col, value);
            case FieldKind.Integer:
                return MakeInteger(col, value);
            case FieldKind.Decimal:
                return MakeDecimal(col, value);
            case FieldKind.Multiline:
                return MakeText(col, value, multiline: true, max: def?.MaxLength ?? 0);
            case FieldKind.Text:
                return MakeText(col, value, multiline: false, max: def?.MaxLength ?? 0);
        }

        // 未定義 → 依欄名與型別猜測
        if (IsBoolCol(col))
            return MakeBool(col, value);
        if (IsDateCol(col))
            return MakeDate(col, value);
        if (clr == typeof(long) && !IsMoneyCol(col.Name))
            return MakeInteger(col, value);
        if (clr == typeof(double))
            return MakeDecimal(col, value);
        if (IsLongTextCol(col))
            return MakeText(col, value, multiline: true, max: 0);
        return MakeText(col, value, multiline: false, max: 0);
    }

    private static CheckBox MakeBool(ColumnInfo col, object? value) => new()
    {
        Text = col.Name,
        Font = UiTheme.Font(10F),
        ForeColor = UiTheme.TextMain,
        AutoSize = true,
        Checked = value is not null && value != DBNull.Value && Convert.ToInt64(value) != 0,
    };

    private static DateTimePicker MakeDate(ColumnInfo col, object? value)
    {
        var dtp = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy/MM/dd",
            Font = UiTheme.Font(10F),
        };
        UiTheme.StyleDateTimePicker(dtp);
        if (value is null || value == DBNull.Value)
        {
            dtp.ShowCheckBox = true;
            dtp.Checked = false;
        }
        else if (value is long l && l > 0 && DateTime.TryParseExact(
            l.ToString(CultureInfo.InvariantCulture),
            new[] { "yyyyMMdd", "yyyyMMddHHmmss", "yyyy/MM/dd", "yyyy/M/d" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtLong))
        {
            dtp.Value = dtLong;
        }
        else if (DateTime.TryParse(Convert.ToString(value, CultureInfo.CurrentCulture), out var dtStr))
        {
            dtp.Value = dtStr;
        }
        else
        {
            dtp.ShowCheckBox = true;
            dtp.Checked = false;
        }
        return dtp;
    }

    private static NumericUpDown MakeInteger(ColumnInfo col, object? value)
    {
        var nud = new NumericUpDown
        {
            Minimum = -2000000000,
            Maximum = 2000000000,
            TextAlign = HorizontalAlignment.Right,
            Font = UiTheme.Font(10F),
            BorderStyle = BorderStyle.FixedSingle,
        };
        if (value is not null && value != DBNull.Value)
            nud.Value = Math.Clamp(Convert.ToInt64(value), (long)nud.Minimum, (long)nud.Maximum);
        return nud;
    }

    private static TextBox MakeDecimal(ColumnInfo col, object? value)
    {
        var tb = new TextBox { TextAlign = HorizontalAlignment.Right, Font = UiTheme.Font(10F) };
        if (value is not null && value != DBNull.Value)
            tb.Text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
        UiTheme.StyleTextBox(tb);
        return tb;
    }

    private static TextBox MakeText(ColumnInfo col, object? value, bool multiline, int max)
    {
        var tb = new TextBox
        {
            Multiline = multiline,
            Height = multiline ? 64 : 27,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
            Font = UiTheme.Font(10F),
            MaxLength = max > 0 ? max : 32767,
        };
        if (value is not null && value != DBNull.Value)
            tb.Text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
        UiTheme.StyleTextBox(tb);
        return tb;
    }

    private static void SetComboValue(ComboBox cb, object? value)
    {
        if (value is null || value == DBNull.Value)
            return;
        cb.SelectedValue = value;
        if (cb.SelectedIndex >= 0)
            return;
        var target = Convert.ToString(value, CultureInfo.InvariantCulture);
        foreach (var item in cb.Items)
        {
            if (item is DataRowView drv &&
                Convert.ToString(drv[cb.ValueMember], CultureInfo.InvariantCulture) == target)
            {
                cb.SelectedValue = drv[cb.ValueMember];
                return;
            }
        }
    }

    /// <summary>下拉選擇後，把來源表的指定欄位值填入本表對應欄位的控制項</summary>
    private static void ApplyLookupCopy(ComboBox cb, IReadOnlyDictionary<string, string> map,
        Dictionary<string, Control> byName)
    {
        if (cb.SelectedItem is not DataRowView drv)
            return;
        foreach (var (target, source) in map)
        {
            if (!byName.TryGetValue(target, out var ctrl))
                continue;
            var val = drv[source];
            if (val is DBNull or null)
                continue;
            switch (ctrl)
            {
                case TextBox tb:
                    tb.Text = Convert.ToString(val, CultureInfo.CurrentCulture) ?? "";
                    break;
                case CheckBox chk:
                    chk.Checked = Convert.ToInt64(val) != 0;
                    break;
                case NumericUpDown nud:
                    try { nud.Value = Math.Clamp(Convert.ToInt64(val), (long)nud.Minimum, (long)nud.Maximum); }
                    catch (Exception) { }
                    break;
                case DateTimePicker dtp:
                    if (DateTime.TryParse(Convert.ToString(val, CultureInfo.CurrentCulture), out var dt))
                        dtp.Value = dt;
                    break;
                case ComboBox targetCb:
                    SetComboValue(targetCb, val);
                    break;
            }
        }
    }

    // ── 欄位特性判斷（未定義欄的猜測） ──
    private static bool IsBoolCol(ColumnInfo col) =>
        col.ClrType == typeof(long) &&
        (col.Name.Contains("停用") || col.Name.Contains("啟用") || col.Name.Contains("有效") ||
         col.Name.Contains("是否") || col.Name.Contains("自動") || col.Name.Contains("核准") ||
         col.Name.Contains("作廢"));

    private static bool IsDateCol(ColumnInfo col) =>
        col.Name.Contains("日期") || col.Name.Contains("年月") || col.Name.Contains("生日") ||
        col.Name.Contains("開票日") || col.Name.Contains("到期日") || col.Name.Contains("預兌日");

    private static bool IsLongTextCol(ColumnInfo col) =>
        col.ClrType == typeof(string) &&
        (col.Name.Contains("備註") || col.Name.Contains("說明") || col.Name.Contains("地址") ||
         col.Name.Contains("摘要") || col.Name.Contains("內容") || col.Name.Contains("附註") ||
         col.Name.Contains("原因") || col.Name.Contains("條件"));

    private static bool IsMoneyCol(string name) =>
        name.Contains("金額") || name.Contains("匯率") || name.Contains("價格") ||
        name.Contains("票面") || name.Contains("成本") || name.Contains("單價");

    // ── 驗證與回寫 ──
    private string? Validate()
    {
        foreach (var (col, ctrl) in _fields)
        {
            bool empty = ctrl switch
            {
                TextBox tb => tb.Text.Trim().Length == 0,
                DateTimePicker dtp => dtp.ShowCheckBox && !dtp.Checked,
                ComboBox cbo => cbo.SelectedIndex < 0,
                _ => false,
            };
            if (!empty)
                continue;
            var def = TableFields.Get(_table.Name)?.FirstOrDefault(d => d.Name == col.Name);
            bool required = def?.Required ?? col.NotNull;
            if (required)
                return $"「{def?.Label ?? col.Name}」為必填欄位。";
            if (!_isNew && col.IsPrimaryKey)
                return $"主鍵欄位「{col.Name}」不可清空。";
        }
        return null;
    }

    private void WriteBack()
    {
        foreach (var (col, ctrl) in _fields)
        {
            object? v;
            switch (ctrl)
            {
                case CheckBox cb:
                    v = cb.Checked ? 1L : 0L;
                    break;
                case DateTimePicker dtp:
                    if (dtp.ShowCheckBox && !dtp.Checked)
                        v = DBNull.Value;
                    else if (col.ClrType == typeof(long))
                        v = dtp.Value.ToString("yyyyMMdd");
                    else
                        v = dtp.Value.ToString("yyyy/MM/dd");
                    break;
                case NumericUpDown nud:
                    v = (long)nud.Value;
                    break;
                case ComboBox cbo:
                    var sv = cbo.SelectedValue;
                    if (sv is null or DBNull)
                        v = DBNull.Value;
                    else if (col.ClrType == typeof(long) && long.TryParse(Convert.ToString(sv, CultureInfo.InvariantCulture), out var lv))
                        v = lv;
                    else
                        v = Convert.ToString(sv, CultureInfo.CurrentCulture);
                    break;
                case TextBox tb:
                    var s = tb.Text.Trim();
                    if (s.Length == 0)
                        v = DBNull.Value;
                    else if (col.ClrType == typeof(double) && decimal.TryParse(s, out var d))
                        v = d;
                    else
                        v = s;
                    break;
                default:
                    continue;
            }
            _row[col.Name] = v;
        }
    }
}
