using System.ComponentModel.DataAnnotations;

namespace RadiKeep.Logics.RdbContext
{
    public class NhkRadiruStation
    {
        /// <summary>
        /// ID
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// エリアID
        /// </summary>
        [MaxLength(10)]
        public string AreaId { get; set; } = string.Empty;

        /// <summary>
        /// エリア名
        /// </summary>
        [MaxLength(10)]
        public string AreaJpName { get; set; } = string.Empty;

        /// <summary>
        /// APIキー
        /// </summary>
        [MaxLength(5)]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// NHKラジオ第1放送 HLS URL
        /// </summary>
        [MaxLength(250)]
        public string R1Hls { get; set; } = string.Empty;

        /// <summary>
        /// NHKラジオ第2放送 HLS URL
        /// </summary>
        [MaxLength(250)]
        public string R2Hls { get; set; } = string.Empty;

        /// <summary>
        /// NHK-FM放送 HLS URL
        /// </summary>
        [MaxLength(250)]
        public string FmHls { get; set; } = string.Empty;

        /// <summary>
        /// 現在放送中の番組情報API URL
        /// </summary>
        [MaxLength(250)]
        public string ProgramNowOnAirApiUrl { get; set; } = string.Empty;

        /// <summary>
        /// 番組詳細情報取得API URLテンプレート
        /// </summary>
        [MaxLength(250)]
        public string ProgramDetailApiUrlTemplate { get; set; } = string.Empty;

        /// <summary>
        /// 番組表API URLテンプレート
        /// </summary>
        [MaxLength(250)]
        public string DailyProgramApiUrlTemplate { get; set; } = string.Empty;
    }
}
