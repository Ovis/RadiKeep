using RadiKeep.Logics.Models;
using RadiKeep.Logics.Models.NhkRadiru;

namespace RadiKeep.Models
{
    public class KeywordReserveViewModel
    {
        public IEnumerable<RadikoStationInformationEntry> RadikoStationList { get; set; } = [];

        public IEnumerable<RadiruStationEntry> RadiruStationList { get; set; } = [];
    }
}
