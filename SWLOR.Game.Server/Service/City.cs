using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SWLOR.Game.Server.Core;
using SWLOR.Game.Server.Core.NWScript;
using SWLOR.Game.Server.Core.NWScript.Enum;
using SWLOR.Game.Server.Entity;
using SWLOR.Game.Server.Extension;
using SWLOR.Game.Server.Service.CityService;
using SWLOR.Game.Server.Service.PerkService;
using static SWLOR.Game.Server.Core.NWScript.NWScript;

namespace SWLOR.Game.Server.Service
{
    public static class City
    {
        private const int OutpostCitizenCount = 5;
        private const int VillageCitizenCount = 10;
        private const int TownshipCitizenCount = 15;
        private const int CityCitizenCount = 30;
        private const int MetropolisCitizenCount = 40;

        private const int ElectionIntervalWeeks = 3;

        private static readonly Dictionary<CityStructureType, CityStructureTypeAttribute> _cityStructures = new Dictionary<CityStructureType, CityStructureTypeAttribute>();

        /// <summary>
        /// When the module loads, cache data and run weekly maintenance
        /// </summary>
        [NWNEventHandler("mod_load")]
        public static void ModuleLoad()
        {
            CacheData();
            WeeklyMaintenance();
            LoadCityStructures();
        }

        /// <summary>
        /// Handles loading and caching data for later use.
        /// </summary>
        private static void CacheData()
        {
            var cityStructureTypes = Enum.GetValues(typeof(CityStructureType)).Cast<CityStructureType>();
            foreach (var cityStructureType in cityStructureTypes)
            {
                if (cityStructureType == CityStructureType.Invalid)
                    continue;

                var detail = cityStructureType.GetAttribute<CityStructureType, CityStructureTypeAttribute>();
                _cityStructures[cityStructureType] = detail;
            }
        }

        /// <summary>
        /// Retrieves details about a particular city structure type.
        /// Will throw an exception if city structure type is not registered.
        /// </summary>
        /// <param name="type">The type of city structure to retrieve</param>
        /// <returns>A city structure detail matching the type.</returns>
        public static CityStructureTypeAttribute GetCityStructure(CityStructureType type)
        {
            return _cityStructures[type];
        }

        /// <summary>
        /// Retrieves all of the city Ids from the database.
        /// </summary>
        /// <returns>A list of city Ids</returns>
        private static List<string> GetAllCityIds()
        {
            return DB.SearchKeys("PlayerCity").ToList();
        }

        /// <summary>
        /// When the module loads, if it is Sunday and it has been a week since the last update,
        /// perform the update now.
        /// </summary>
        private static void WeeklyMaintenance()
        {
            var serverConfig = DB.Get<ServerConfiguration>("SWLOR") ?? new ServerConfiguration();
            var now = DateTime.UtcNow;
            var neverRanBefore = serverConfig.DateLastCityMaintenance == DateTime.MinValue;

            // A two-hour grace period is added to the current time to account for time zone differences.
            var weeklyUpdateShouldRun = now.DayOfWeek == DayOfWeek.Sunday &&
                                        (now.AddHours(2) - serverConfig.DateLastCityMaintenance).Days >= 7;

            // If this is the very first time it's run, or today is Sunday and the
            // last run is a week old
            if (neverRanBefore || weeklyUpdateShouldRun)
            {
                Log.Write(LogGroup.City, $"*****WEEKLY CITY MAINTENANCE BEGINNING*****", true);

                var cityIds = GetAllCityIds();

                foreach (var cityId in cityIds)
                {
                    try
                    {
                        var city = DB.Get<PlayerCity>(cityId);
                        Log.Write(LogGroup.City, $"Processing city ID: {cityId}, Name: {city.Name}", true);
                        var citizenPlayers = BuildCitizenPlayerList(city).ToList();
                        var mayorPlayer = DB.Get<Player>(city.MayorPlayerId);

                        var cityTornDown = ProcessCityRank(city, mayorPlayer, citizenPlayers);

                        if(!cityTornDown)
                            cityTornDown = ProcessTaxes(city, citizenPlayers);

                        if(!cityTornDown)
                            cityTornDown = ProcessMaintenance(city, citizenPlayers);

                        if(!cityTornDown)
                            cityTornDown = ProcessElection(city, mayorPlayer);

                        if(!cityTornDown)
                            DB.Set(cityId, city);

                        Log.Write(LogGroup.City, $"Finished processing city ID: {cityId}, Name: {city.Name}", true);
                    }
                    catch (Exception ex)
                    {
                        Log.Write(LogGroup.City, $"Failed to process city ID: {cityId}. Exception: {ex.ToMessageAndCompleteStacktrace()}", true);
                    }
                }

                serverConfig.DateLastCityMaintenance = now;
                DB.Set("SWLOR", serverConfig);
                Log.Write(LogGroup.City, $"*****WEEKLY CITY MAINTENANCE COMPLETED*****", true);
            }
        }
        
        /// <summary>
        /// Retrieves all database player records for a particular city's citizens.
        /// </summary>
        /// <param name="city">The city to retrieve from.</param>
        /// <returns>A list of player database records.</returns>
        private static IEnumerable<Player> BuildCitizenPlayerList(PlayerCity city)
        {
            foreach (var citizen in city.CitizenPermissions)
            {
                yield return DB.Get<Player>(citizen.Key);
            }
        }

        private static bool ProcessCityRank(PlayerCity city, Player mayorPlayer, List<Player> citizenPlayers)
        {
            Log.Write(LogGroup.City, $"Processing city rank...");

            // Get the citizen count, but only include the same CD Key once.
            var citizenCount = citizenPlayers
                .GroupBy(g => g.LastCDKey)
                .Select(s => s.First())
                .Count();
            var mayorCityManagementRank = mayorPlayer.Perks.ContainsKey(PerkType.CityManagement)
                ? mayorPlayer.Perks[PerkType.CityManagement]
                : 0;

            if (citizenCount >= MetropolisCitizenCount && mayorCityManagementRank >= 4)
                city.Rank = CityRankType.Metropolis;
            else if (citizenCount >= CityCitizenCount && mayorCityManagementRank >= 3)
                city.Rank = CityRankType.City;
            else if (citizenCount >= TownshipCitizenCount && mayorCityManagementRank >= 2)
                city.Rank = CityRankType.Township;
            else if (citizenCount >= VillageCitizenCount && mayorCityManagementRank >= 1)
                city.Rank = CityRankType.Village;
            else if (citizenCount >= OutpostCitizenCount && mayorCityManagementRank >= 1)
                city.Rank = CityRankType.Outpost;
            else
                city.Rank = CityRankType.Invalid;

            // City rank is invalid which means either there are insufficient citizens
            // or the mayor's perks have dropped too low.
            // Tear the city down.
            if (city.Rank == CityRankType.Invalid)
            {
                Log.Write(LogGroup.City, $"Tearing down city: {city.Name} due to insufficient citizens or lack of mayor city management perk. (Mayor perk level: {mayorCityManagementRank})");
                TearDownCity(city);

                return true;
            }

            return false;
        }

        private static bool ProcessTaxes(PlayerCity city, List<Player> citizenPlayers)
        {
            Log.Write(LogGroup.City, $"Processing city taxes...");

            return false;
        }

        private static bool ProcessMaintenance(PlayerCity city, List<Player> citizenPlayers)
        {
            Log.Write(LogGroup.City, $"Processing city maintenance...");

            var cityHallStructure = GetCityStructure(CityStructureType.CityHall);
            
            // Not enough money in the Treasury to afford city hall's maintenance cost.
            // Destroy the city.
            if (city.Treasury < cityHallStructure.MaintenanceCost)
            {
                Log.Write(LogGroup.City, $"Tearing down city: {city.Name} due to insufficient money in the treasury to cover City Hall's maintenance.");
                TearDownCity(city);
                return true;
            }

            Log.Write(LogGroup.City, $"Paid maintenance cost for city hall.");
            city.Treasury -= cityHallStructure.MaintenanceCost;

            foreach (var (_, detail) in city.Structures)
            {
                var structure = GetCityStructure(detail.Type);

                if (city.Treasury < structure.MaintenanceCost)
                {
                    ReduceStructureDamage(detail);
                    Log.Write(LogGroup.City, $"Insufficient treasury funds to pay maintenance cost on {detail.Name} [{structure.Name}]. Damage state reduced to: {detail.DamageLevel}");
                }
            }

            // Remove invalid and destroyed structures.
            var previousStructureCount = city.Structures.Count;
            city.Structures = city.Structures.Where(x =>
                x.Value.DamageLevel == CityStructureDamageType.Undamaged ||
                x.Value.DamageLevel == CityStructureDamageType.Half)
                .ToDictionary(x => x.Key, y => y.Value);
            var postCleanupStructureCount = city.Structures.Count;
            var structuresRemoved = previousStructureCount - postCleanupStructureCount;

            Log.Write(LogGroup.City, $"Removed {structuresRemoved} structures that were destroyed or invalid.");

            return false;
        }

        /// <summary>
        /// Reduces a structure's damage level by one.
        /// </summary>
        /// <param name="structure">The structure to damage.</param>
        private static void ReduceStructureDamage(CityStructure structure)
        {
            switch (structure.DamageLevel)
            {
                case CityStructureDamageType.Undamaged:
                    structure.DamageLevel = CityStructureDamageType.Half;
                    break;
                case CityStructureDamageType.Half:
                    structure.DamageLevel = CityStructureDamageType.Destroyed;
                    break;
                default:
                    structure.DamageLevel = CityStructureDamageType.Invalid;
                    break;
            }
        }

        /// <summary>
        /// Processes election state.
        /// A new election is started once every 3 weeks.
        /// Candidates can sign up during the first two weeks.
        /// Voting occurs on the last week.
        /// If there's a tie between the winners, the incumbent remains mayor.
        /// </summary>
        /// <param name="city"></param>
        /// <param name="mayorPlayer"></param>
        private static bool ProcessElection(PlayerCity city, Player mayorPlayer)
        {
            Log.Write(LogGroup.City, $"Processing city election...");

            // Election is inactive. Increase weeks.
            // If weeks >= ElectionIntervalWeeks, an election should start.
            if (city.ElectionDetail.CurrentMode == ElectionModeType.Inactive)
            {
                city.ElectionDetail.WeeksSinceLastElection++;

                if (city.ElectionDetail.WeeksSinceLastElection >= ElectionIntervalWeeks)
                {
                    Log.Write(LogGroup.City, $"A new election has started!");
                    city.ElectionDetail.CurrentMode = ElectionModeType.RegistrationWeek1;
                }
            }
            // Week 1 Registration ended. Move to Week 2 Registration mode.
            else if (city.ElectionDetail.CurrentMode == ElectionModeType.RegistrationWeek1)
            {
                Log.Write(LogGroup.City, $"Second week of registration started.");
                city.ElectionDetail.CurrentMode = ElectionModeType.RegistrationWeek2;
            }
            // Week 2 Registration ended. Move to Voting mode.
            else if (city.ElectionDetail.CurrentMode == ElectionModeType.RegistrationWeek2)
            {
                Log.Write(LogGroup.City, $"Voting week has started.");
                city.ElectionDetail.CurrentMode = ElectionModeType.Voting;

                // If no one runs against the incumbent, exit voting early
                if (city.ElectionDetail.Candidates.Count <= 1)
                {
                    Log.Write(LogGroup.City, $"Incumbent mayor {mayorPlayer.Name} ran unopposed and wins the election.");
                    city.ElectionDetail.CurrentMode = ElectionModeType.Inactive;
                    city.ElectionDetail.WeeksSinceLastElection = 0;
                }
            }
            // Voting week ended. Tally the votes and make the adjustments.
            else if (city.ElectionDetail.CurrentMode == ElectionModeType.Voting)
            {
                var incumbentMayor = city.MayorPlayerId;
                var winnerPlayerId = incumbentMayor;

                if (city.ElectionDetail.Candidates.Count <= 1)
                {
                    var votes = new Dictionary<string, int>();
                    foreach (var vote in city.ElectionDetail.PlayerVotes.Values)
                    {
                        if (!votes.ContainsKey(vote))
                            votes[vote] = 0;

                        votes[vote]++;
                    }

                    var orderedVotes = votes.OrderByDescending(x => x.Value).ToList();
                    // If top two are the same, incumbent mayor wins. Otherwise, take the person with the highest votes.
                    if (orderedVotes.ElementAt(0).Value != orderedVotes.ElementAt(1).Value)
                    {
                        winnerPlayerId = orderedVotes.ElementAt(0).Key;
                    }
                    else
                    {
                        Log.Write(LogGroup.City, $"Top 2 candidates were tied. Incumbent mayor wins.");
                    }

                    Log.Write(LogGroup.City, $"Vote Counts:");
                    foreach (var (candidatePlayerId, voteCount) in orderedVotes)
                    {
                        Log.Write(LogGroup.City, $"{candidatePlayerId}: {voteCount} votes");
                    }
                }
                else
                {
                    Log.Write(LogGroup.City, $"Incumbent ran unopposed. No need to tally votes.");
                }

                city.MayorPlayerId = winnerPlayerId;
                city.ElectionDetail.CurrentMode = ElectionModeType.Inactive;
                city.ElectionDetail.WeeksSinceLastElection = 0;
                city.ElectionDetail.PlayerVotes.Clear();
                city.ElectionDetail.Candidates.Clear();

                var oldMayorPlayer = DB.Get<Player>(incumbentMayor);
                var newMayorPlayer = DB.Get<Player>(city.MayorPlayerId);

                Log.Write(LogGroup.City, $"Election completed. Old Mayor ({incumbentMayor}): {oldMayorPlayer.Name}, New Mayor ({city.MayorPlayerId}): {newMayorPlayer.Name}");
            }

            return false;
        }

        /// <summary>
        /// Completely tears down a city and all its structures.
        /// All credits, items, etc. will be destroyed immediately.
        /// </summary>
        /// <param name="city">The city to tear down</param>
        private static void TearDownCity(PlayerCity city)
        {
            // todo: remove housing info
            DB.Delete<PlayerCity>(city.ID.ToString());
        }

        /// <summary>
        /// Cycles through every player city and loads the associated structures into the world.
        /// </summary>
        private static void LoadCityStructures()
        {
            var cityIds = GetAllCityIds();

            foreach (var cityId in cityIds)
            {
                var city = DB.Get<PlayerCity>(cityId);

                foreach (var (id, structure) in city.Structures)
                {
                    var structureDetail = GetCityStructure(structure.Type);
                    var area = Cache.GetAreaByResref(structure.AreaResref);
                    var position = new Vector3(structure.X, structure.Y, structure.Z);
                    var location = Location(area, position, structure.Orientation);

                    var placeable = CreateObject(ObjectType.Placeable, structureDetail.Resref, location);

                    SetLocalString(placeable, "CITY_ID", cityId);
                    SetLocalString(placeable, "STRUCTURE_ID", id.ToString());
                }

                Log.Write(LogGroup.City, $"Loaded {city.Structures.Count} structures for player city '{city.Name}'.", true);
            }

        }

    }
}
