using System;
using System.Collections.Generic;
using SWLOR.Game.Server.Service.CityService;

namespace SWLOR.Game.Server.Entity
{
    public class PlayerCity: EntityBase
    {
        public PlayerCity()
        {
            Structures = new Dictionary<Guid, CityStructure>();
            CitizenPermissions = new Dictionary<string, CityPermission>();
            ElectionDetail = new CityElectionDetail();
            TaxDetail = new CityTaxDetail();
        }

        public override string KeyPrefix => "PlayerCity";

        public string Name { get; set; }
        public int WeeklyMaintenanceFee { get; set; }
        public string MayorPlayerId { get; set; }
        public string FounderPlayerId { get; set; }
        public DateTime DateFounded { get; set; }
        public CityRankType Rank { get; set; }
        public int Treasury { get; set; }
        public Dictionary<Guid, CityStructure> Structures { get; set; }
        public Dictionary<string, CityPermission> CitizenPermissions { get; set; }
        public CityElectionDetail ElectionDetail { get; set; }
        public CityTaxDetail TaxDetail { get; set; }
    }

    public class CityStructure
    {
        public string Name { get; set; }
        public CityStructureType Type { get; set; }
        public CityStructureDamageType DamageLevel { get; set; }
        public string AreaResref { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Orientation { get; set; }

        public Guid OwnerPlayerId { get; set; }
    }

    public class CityElectionDetail
    {
        public CityElectionDetail()
        {
            Candidates = new HashSet<string>();
            CurrentMode = ElectionModeType.Inactive;
            PlayerVotes = new Dictionary<string, string>();
        }

        public int WeeksSinceLastElection { get; set; }
        public ElectionModeType CurrentMode { get; set; }
        public HashSet<string> Candidates { get; set; }
        public Dictionary<string, string> PlayerVotes { get; set; }
    }
    
    public class CityTaxDetail
    {
        public int WeeklyFee { get; set; }
        public int TravelFee { get; set; }
        public float PropertyTax { get; set; }
    }

    public class CityPermission
    {
        public bool CanDonateToCityTreasury { get; set; }
        public bool CanUseTravelServices { get; set; }
        public bool CanUseBankingServices { get; set; }
        public bool CanUseMedicalServices { get; set; }

    }
}
