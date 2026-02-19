using System.ComponentModel.DataAnnotations;

namespace RadiKeep.Logics.RdbContext
{
    public class KeywordReserveTagRelation
    {
        /// <summary>
        /// ID
        /// </summary>
        [Required]
        [Key]
        [MaxLength(26)]
        public Ulid ReserveId { get; set; }

        public KeywordReserve KeywordReserve { get; set; } = null!;

        /// <summary>
        /// タグID
        /// </summary>
        public Guid TagId { get; set; }

        public RecordingTag Tag { get; set; } = null!;
    }
}
