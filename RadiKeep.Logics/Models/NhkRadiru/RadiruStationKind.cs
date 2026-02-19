using RadiKeep.Logics.Primitives;

namespace RadiKeep.Logics.Models.NhkRadiru
{
    public class RadiruStationKind : Enumeration
    {
        public static readonly RadiruStationKind R1 = new(
            id: 1,
            name: "NHKラジオ第1",
            serviceId: "r1");

        public static readonly RadiruStationKind R2 = new(
            id: 2,
            name: "NHKラジオ第2",
            serviceId: "r2");

        public static readonly RadiruStationKind FM = new(
            id: 3,
            name: "NHK-FM",
            serviceId: "r3");



        public string ServiceId { get; set; }

        /// <summary>
        /// らじる★らじるの局種別を初期化する
        /// </summary>
        /// <param name="id">ID</param>
        /// <param name="name">表示名</param>
        public RadiruStationKind(int id, string name) : base(id, name)
        {
            ServiceId = string.Empty;
        }

        public RadiruStationKind(int id, string name, string serviceId) : base(id, name)
        {
            ServiceId = serviceId;
        }
    }
}
