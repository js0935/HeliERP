using HeliERP.App;

/// <summary>重疊對（mm 座標，實測文字矩形）。</summary>
internal record struct OverlapPair(
    RtmComponent A, RtmComponent B,
    float Ax, float Ay, float Aw, float Ah,
    float Bx, float By, float Bw, float Bh,
    float Ox, float Oy, float Area, string Ta, string Tb);
