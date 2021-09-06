using System;

namespace SWLOR.Game.Server.Service.CityService
{
    public enum CityStructureType
    {
        [CityStructureType("Invalid", "", 0, false)]
        Invalid = 0,
        [CityStructureType("City Hall", "", 5000, true)]
        CityHall = 1,
        [CityStructureType("Bank", "", 3000, true)]
        Bank = 2,
        [CityStructureType("Medical Center", "", 4500, true)]
        MedicalCenter = 3,
        [CityStructureType("Starport", "", 8000, true)]
        Starport = 4,
        [CityStructureType("Cantina", "", 2000, true)]
        Cantina = 5,
        [CityStructureType("House", "", 1000, true)]
        House = 6
    }

    public class CityStructureTypeAttribute : Attribute
    {
        public CityStructureTypeAttribute(string name, string resref, int maintenanceCost, bool isActive)
        {
            Name = name;
            Resref = resref;
            MaintenanceCost = maintenanceCost;
            IsActive = isActive;
        }

        public string Name { get; set; }
        public string Resref { get; set; }
        public int MaintenanceCost { get; set; }
        public bool IsActive { get; set; }
    }

}
