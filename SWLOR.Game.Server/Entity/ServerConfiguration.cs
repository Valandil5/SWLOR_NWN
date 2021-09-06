using System;

namespace SWLOR.Game.Server.Entity
{
    public class ServerConfiguration: EntityBase
    {
        public ServerConfiguration()
        {
            MigrationVersion = 0;
            DateLastRestart = DateTime.MinValue;
            DateLastCityMaintenance = DateTime.MinValue;
        }

        public int MigrationVersion { get; set; }
        public DateTime DateLastRestart { get; set; }
        public DateTime DateLastCityMaintenance { get; set; }
        public override string KeyPrefix => "ServerConfiguration";
    }
}
