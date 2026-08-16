// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Text;

namespace HeliERP.App;

/// <summary>Delphi 二進位 form stream（TPF0）解析後的物件樹節點。</summary>
public sealed class Tpf0Object
{
    public string ClassName { get; init; } = "";
    public string Name { get; init; } = "";
    public long Offset { get; init; }
    public List<(string Name, object? Value)> Properties { get; } = new();
    public List<Tpf0Property> PropertiesEx { get; } = new();
    public List<Tpf0Object> Children { get; } = new();
}

/// <summary>屬性值記錄：含值位元組偏移與 tag（供原地二進位修補）。</summary>
public sealed class Tpf0Property
{
    public required string Name { get; init; }
    public required object? Value { get; init; }
    public required int ValueOffset { get; init; }
    public required byte Tag { get; init; }
    public required int ValuePayloadStart { get; init; }
    public required int ValuePayloadLength { get; init; }
}

/// <summary>vaNil 的哨兵值。</summary>
public sealed class NilValue
{
    public static readonly NilValue Instance = new();
    private NilValue() { }
}

public sealed class Tpf0ParseException : Exception
{
    public Tpf0ParseException(string message) : base(message) { }
}

/// <summary>
/// 自行重寫的 TPF0 二進位 form stream 解析器。
/// 規格來源：continuous-delphi/delphi-forms-parser 的 docs/tpf0-binary-format.md（MIT）。
/// .rtm（ReportBuilder 報表範本）即 TPF0 格式。
/// </summary>
public static class Tpf0Reader
{
    static Tpf0Reader()
    {
        // .NET Core 起需註冊 code page provider 才能用 CP950（Big5）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private sealed class Reader
    {
        private readonly byte[] _data;
        private readonly Encoding _ansi;
        public int Pos;
        public string CurrentProp = "";
        public List<string> Trace = new();

        public Reader(byte[] data)
        {
            _data = data;
            // .rtm 內 ANSI 字串在繁體中文系統為 Big5（CP950）
            _ansi = Encoding.GetEncoding(950, EncoderFallback.ReplacementFallback,
                DecoderFallback.ReplacementFallback);
        }

        public byte ReadByte() => _data[Pos++];
        public byte PeekByte() => _data[Pos];

        public short ReadInt16() { var v = BitConverter.ToInt16(_data, Pos); Pos += 2; return v; }
        public int ReadInt32() { var v = BitConverter.ToInt32(_data, Pos); Pos += 4; return v; }
        public long ReadInt64() { var v = BitConverter.ToInt64(_data, Pos); Pos += 8; return v; }
        public float ReadSingle() { var v = BitConverter.ToSingle(_data, Pos); Pos += 4; return v; }
        public double ReadDouble() { var v = BitConverter.ToDouble(_data, Pos); Pos += 8; return v; }
        public byte[] ReadBytes(int n) { var v = _data.AsSpan(Pos, n).ToArray(); Pos += n; return v; }
        public string ReadAnsi(int len) => _ansi.GetString(_data, Pos, len);

        public string ReadShortString()
        {
            int len = ReadByte();
            var s = ReadAnsi(len);
            Pos += len;
            return s;
        }

        public Tpf0Object ReadObject()
        {
            int start = Pos;
            int clsLenByte = ReadByte();
            // class 名長度是整個 byte（Delphi 短字串長度，0-255），不是低位 nibble！
            int clsLen = clsLenByte;
            string clsName = ReadAnsi(clsLen);
            Pos += clsLen;
            Trace.Add($"OBJ@{start}: clsLenByte=0x{clsLenByte:X2} cls='{clsName}'");
            int objNameLenAt = Pos;
            string name = ReadShortString();
            Trace.Add($"   name@{objNameLenAt} len={name.Length} '{name}'");
            var obj = new Tpf0Object { ClassName = clsName, Name = name, Offset = start };

            // Properties：直到 name 長度為 0
            while (true)
            {
                int nameLenAt = Pos;
                int nameLen = ReadByte();
                if (nameLen == 0) break;
                string propName = ReadAnsi(nameLen);
                Pos += nameLen;
                CurrentProp = propName;
                Trace.Add($"{nameLenAt}:{propName}");
                int valStart = Pos;
                object? value = ReadValue();
                obj.Properties.Add((propName, value));
                obj.PropertiesEx.Add(new Tpf0Property
                {
                    Name = propName,
                    Value = value,
                    ValueOffset = valStart,
                    Tag = _data[valStart],
                    ValuePayloadStart = valStart + 1,
                    ValuePayloadLength = Pos - valStart - 1,
                });
            }

            // Children：直到下一個 byte 為 0（end-of-children sentinel）
            while (Pos < _data.Length && PeekByte() != 0)
            {
                obj.Children.Add(ReadObject());
            }
            if (Pos < _data.Length && PeekByte() == 0) Pos++;
            return obj;
        }

        public object? ReadValue()
        {
            int tagOffset = Pos;
            byte tag = ReadByte();
            switch (tag)
            {
                case 0x01: // vaList：值序列 + $00
                {
                    var list = new List<object?>();
                    while (true)
                    {
                        if (PeekByte() == 0) { Pos++; break; }
                        list.Add(ReadValue());
                    }
                    return list;
                }
                case 0x02: return (long)(sbyte)ReadByte();          // vaInt8
                case 0x03: return (long)ReadInt16();                // vaInt16
                case 0x04: return (long)ReadInt32();                // vaInt32
                case 0x05: return Extended80ToDouble(ReadBytes(10));// vaExtended
                case 0x06: return ReadShortString();                // vaString
                case 0x07: return ReadShortString();                // vaIdent
                case 0x08: return false;                            // vaFalse
                case 0x09: return true;                             // vaTrue
                case 0x0A:                                          // vaBinary
                {
                    int len = ReadInt32();
                    if (len < 0 || len > _data.Length - Pos)
                        throw new Tpf0ParseException($"Binary 長度異常 {len} @ offset {tagOffset}");
                    return ReadBytes(len);
                }
                case 0x0B:                                          // vaSet
                {
                    var set = new List<string>();
                    while (true)
                    {
                        int len = ReadByte();
                        if (len == 0) break;
                        set.Add(ReadAnsi(len));
                        Pos += len;
                    }
                    return set;
                }
                case 0x0C:                                          // vaLString
                {
                    int len = ReadInt32();
                    if (len < 0 || len > _data.Length - Pos)
                        throw new Tpf0ParseException($"LString 長度異常 {len} @ offset {tagOffset}");
                    var s = ReadAnsi(len);
                    Pos += len;
                    return s;
                }
                case 0x0D: return NilValue.Instance;               // vaNil
                case 0x0E:                                          // vaCollection
                {
                    var items = new List<(object? Index, List<(string Name, object? Value)> Props)>();
                    while (true)
                    {
                        if (PeekByte() == 0) { Pos++; break; }
                        object? index = ReadValue();
                        var props = new List<(string, object?)>();
                        while (true)
                        {
                            int nameLen = ReadByte();
                            if (nameLen == 0) break;
                            string pn = ReadAnsi(nameLen);
                            Pos += nameLen;
                            object? pv = ReadValue();
                            props.Add((pn, pv));
                        }
                        items.Add((index, props));
                    }
                    return items;
                }
                case 0x0F: return ReadSingle();                    // vaSingle
                case 0x10: return ReadInt64() / 10000.0;           // vaCurrency
                case 0x11: return ReadDouble();                    // vaDate（TDateTime double）
                case 0x12:                                          // vaWString
                {
                    int charCount = ReadInt32();
                    var s = Encoding.Unicode.GetString(_data, Pos, charCount * 2);
                    Pos += charCount * 2;
                    return s;
                }
                case 0x13: return ReadInt64();                     // vaInt64
                case 0x14:                                          // vaUTF8String（4-byte bytelen + UTF-8）
                {
                    int len = ReadInt32();
                    if (len < 0 || len > _data.Length - Pos)
                        throw new Tpf0ParseException($"UTF8String 長度異常 {len} @ offset {tagOffset}");
                    var s = Encoding.UTF8.GetString(_data, Pos, len);
                    Pos += len;
                    return s;
                }
                case 0x15: return ReadDouble();                    // vaDouble
                case 0x16:                                          // vaUString（4-byte charcount + UTF-16LE）
                {
                    int charCount = ReadInt32();
                    var s = Encoding.Unicode.GetString(_data, Pos, charCount * 2);
                    Pos += charCount * 2;
                    return s;
                }
                case 0x17:                                          // vaAny：1-byte 子型別 + 遞迴值
                {
                    _ = ReadByte();
                    return ReadValue();
                }
                case 0x18: return (long)(sbyte)ReadByte();         // vaInt8Literal
                case 0x19: return (long)ReadInt16();               // vaInt16Literal
                case 0x1A: return (long)ReadInt32();               // vaInt32Literal
                case 0x1B: return ReadObject();                    // vaObject：完整子物件
                case 0x1C: return ReadVariant();                   // vaVariant
                case 0x1D: return null;                            // vaNullVariant
                case 0x1E:                                          // vaTypeAlias：short string + 遞迴值
                {
                    _ = ReadShortString();
                    return ReadValue();
                }
                case 0x1F:                                          // vaUTF8StringLiteral
                {
                    int len = ReadInt32();
                    var s = Encoding.UTF8.GetString(_data, Pos, len);
                    Pos += len;
                    return s;
                }
                case 0x20:                                          // vaUStringLiteral
                {
                    int charCount = ReadInt32();
                    var s = Encoding.Unicode.GetString(_data, Pos, charCount * 2);
                    Pos += charCount * 2;
                    return s;
                }
                default:
                {
                    int from = Math.Max(0, tagOffset - 250);
                    int to = Math.Min(_data.Length, tagOffset + 64);
                    var hex = string.Join(" ", _data[from..to].Select(b => b.ToString("X2")));
                    var trace = string.Join(" | ", Trace.TakeLast(40));
                    throw new Tpf0ParseException(
                        $"未知值型別 tag $0x{tag:X2} @ offset {tagOffset}，屬性: {CurrentProp}\n" +
                        $"hex[{from}..{to}): {hex}\ntrace: {trace}");
                }
            }
        }

        /// <summary>vaVariant：1-byte varType + 依型別的 payload（Delphi 7 TReader.ReadVariant）。</summary>
        public object? ReadVariant()
        {
            int vtOffset = Pos;
            int vt = ReadByte();
            switch (vt)
            {
                case 0x00: // varEmpty
                case 0x01: // varNull
                    return null;
                case 0x02: // varSmallint
                    return (long)ReadInt16();
                case 0x03: // varInteger
                    return (long)ReadInt32();
                case 0x04: // varSingle
                    return ReadSingle();
                case 0x05: // varDouble
                    return ReadDouble();
                case 0x06: // varCurrency
                    return ReadInt64() / 10000.0;
                case 0x07: // varDate（TDateTime double）
                    return ReadDouble();
                case 0x08: // varOleStr：4-byte charcount + UTF-16LE
                {
                    int charCount = ReadInt32();
                    if (charCount < 0 || charCount * 2 > _data.Length - Pos)
                        throw new Tpf0ParseException($"Variant OleStr 長度異常 {charCount} @ offset {vtOffset}");
                    var s = Encoding.Unicode.GetString(_data, Pos, charCount * 2);
                    Pos += charCount * 2;
                    return s;
                }
                case 0x0B: // varBoolean：1 byte（0/1）
                    return ReadByte() != 0;
                case 0x0C: // varVariant：遞迴
                    return ReadVariant();
                case 0x10: // varShortInt
                    return (long)(sbyte)ReadByte();
                case 0x11: // varByte
                    return (long)ReadByte();
                case 0x12: // varWord
                {
                    var v = BitConverter.ToUInt16(_data, Pos); Pos += 2; return (long)v;
                }
                case 0x13: // varLongWord
                {
                    var v = BitConverter.ToUInt32(_data, Pos); Pos += 4; return (long)v;
                }
                case 0x14: // varInt64
                    return ReadInt64();
                default:
                {
                    int from = Math.Max(0, vtOffset - 120);
                    int to = Math.Min(_data.Length, vtOffset + 64);
                    var hex = string.Join(" ", _data[from..to].Select(b => b.ToString("X2")));
                    var trace = string.Join(" | ", Trace.TakeLast(40));
                    throw new Tpf0ParseException(
                        $"未知 Variant varType $0x{vt:X2} @ offset {vtOffset}，屬性: {CurrentProp}\n" +
                        $"hex[{from}..{to}): {hex}\ntrace: {trace}");
                }
            }
        }

        /// <summary>Intel 80-bit extended（10 bytes, little-endian）轉 double。</summary>
        public static double Extended80ToDouble(byte[] b)
        {
            ulong mantissa = BitConverter.ToUInt64(b, 0);
            ushort expWord = BitConverter.ToUInt16(b, 8);
            bool neg = (expWord & 0x8000) != 0;
            int exp = expWord & 0x7FFF;
            if (exp == 0x7FFF) return neg ? double.NegativeInfinity : double.PositiveInfinity;
            if (exp == 0 && mantissa == 0) return neg ? -0.0 : 0.0;
            double v = Math.ScaleB((double)mantissa, exp - 16383 - 63);
            return neg ? -v : v;
        }
    }

    /// <summary>解析 .rtm（TPF0）檔，回傳根物件。</summary>
    public static Tpf0Object Parse(byte[] data)
    {
        int pos = 0;
        if (data.Length >= 5 && data[0] == 0xFF && data[1] == (byte)'T' && data[2] == (byte)'P'
            && data[3] == (byte)'F' && data[4] == (byte)'0')
        {
            pos = 5; // 標準：$FF + "TPF0"
        }
        else if (data.Length >= 4 && data[0] == (byte)'T' && data[1] == (byte)'P'
            && data[2] == (byte)'F' && data[3] == (byte)'0')
        {
            pos = 4; // bare："TPF0"
        }
        else
        {
            throw new Tpf0ParseException("不是有效的 TPF0 檔頭（缺少 TPF0 簽名）");
        }

        var r = new Reader(data) { Pos = pos };
        return r.ReadObject();
    }
}
