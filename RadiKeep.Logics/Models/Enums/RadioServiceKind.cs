using RadiKeep.Logics.Primitives.DataAnnotations;

namespace RadiKeep.Logics.Models.Enums
{
    /// <summary>
    /// 配信サービス種別
    /// </summary>
    public enum RadioServiceKind
    {
        [EnumDisplayName("未指定")]
        Undefined = 0,

        [EnumDisplayName("radiko")]
        [CodeId("Radiko")]
        Radiko = 1,

        [EnumDisplayName("らじる\u2605らじる")]
        [CodeId("Radiru")]
        Radiru = 2,

        [EnumDisplayName("その他")]
        Other = 99,
    }
}
