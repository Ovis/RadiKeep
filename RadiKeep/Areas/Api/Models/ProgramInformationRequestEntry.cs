using System.ComponentModel.DataAnnotations;
using RadiKeep.Logics.Models.Enums;

namespace RadiKeep.Areas.Api.Models
{
    public class ProgramInformationRequestEntry
    {
        /// <summary>
        /// 番組ID
        /// </summary>
        [Required]
        public string ProgramId { get; set; } = string.Empty;

        /// <summary>
        /// 配信サービス種別
        /// </summary>
        public RadioServiceKind RadioServiceKind { get; set; }

        /// <summary>
        /// 録音方法
        /// </summary>
        public RecordingType RecordingType { get; set; }
    }

    public class UpdateRadikoLoginEntity
    {
        /// <summary>
        /// radikoログインユーザーID
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// radikoログインパスワード
        /// </summary>
        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
