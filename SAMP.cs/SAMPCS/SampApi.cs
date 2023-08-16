﻿using System;
using System.Collections.Generic;

namespace SAMPCS
{
    public static class SampApi
    {
        #region GTA SA
        private const int ADDR_PLAYER_IN_MENU = 0xBA67A4; 
        private const int ADDR_CURRENT_INTERIOR_ID = 0xA4ACE8; 
        private const int ADDR_CURRENT_VEHICLE_ID = 0xBA18FC; 

        private const int ADDR_CPED_PTR = 0xB6F5F0; 
        private const int PED_FACING_ANGLE_OFFSET = 0x558; 

        
        private const int ADDR_POSITION_X = 0xB6F2E4;
        private const int ADDR_POSITION_Y = 0xB6F2E8;
        private const int ADDR_POSITION_Z = 0xB6F2EC;
        #endregion

        #region SAMP
        
        
        private static readonly int[] ADDR_SAMP_CHATMSG_PTR = { 0x21A0E4, 0x21A0EC, 0x2ACA10 };
        private static readonly int[] ADDR_SAMP_INCHAT_PTR = { 0x21A10C, 0x21A114, 0x2ACA3C };
        private static readonly int[] ADDR_SAMP_INCHAT_PTR_OFFSET = { 0x55, 0x60, 0x60 };


        
        
        private static readonly int[] FUNC_SAMP_ADDTOCHATWND = { 0x64520, 0x645F0, 0x67B60 };
        
        private static readonly int[] FUNC_SAMP_SENDSAY = { 0x57F0, 0x57E0, 0x5860 };
        private static readonly int[] FUNC_SAMP_SENDCMD = { 0x65C60, 0x65D30, 0x69340 };
        #endregion

        private static List<Zone> _zones; 
        private static List<Zone> _cities; 

        public static int? GtaProcessId { set; private get; }

        public static Coordinates? GetPlayerCoordinates()
        {
            if (!Internal.CheckHandles(GtaProcessId))
            {
                return null;
            }

            var coordinates = new Coordinates
            {
                X = Internal.ReadFloat(Internal.GtaBaseAddress, new IntPtr(ADDR_POSITION_X)),
                Y = Internal.ReadFloat(Internal.GtaBaseAddress, new IntPtr(ADDR_POSITION_Y)),
                Z = Internal.ReadFloat(Internal.GtaBaseAddress, new IntPtr(ADDR_POSITION_Z))
            };

            return coordinates;
        }

        public static string GetPlayerCurrentZone()
        {
            InitializeZonesAndCities();
            var playerCoordinates = GetPlayerCoordinates();

            foreach (var zone in _zones)
            {
                var zoneMinCoordinates = new Coordinates() {X = zone.X1, Y = zone.Y1, Z = zone.Z1};
                var zoneMaxCoordinates = new Coordinates() { X = zone.X2, Y = zone.Y2, Z = zone.Z2 };

                if (Intersects(playerCoordinates.Value, zoneMinCoordinates, zoneMaxCoordinates))
                {
                    return zone.Name;
                }
            }

            return "Unknown";
        }

        public static bool IsPlayerInEscapeMenu()
        {
            if (!Internal.CheckHandles(GtaProcessId))
            {
                return false;
            }

            var isPlayerInMenu = Internal.ReadDword(Internal.GtaBaseAddress, new IntPtr(ADDR_PLAYER_IN_MENU));
            return Convert.ToBoolean(isPlayerInMenu);
        }

        public static bool IsPlayerInAnyInterior()
        {
            if (!Internal.CheckHandles(GtaProcessId))
            {
                return false;
            }

            var interiorId = Internal.ReadDword(Internal.GtaBaseAddress, new IntPtr(ADDR_CURRENT_INTERIOR_ID));
            return interiorId > 0;
        }

        public static bool IsPlayerInAnyVehicle()
        {
            if (!Internal.CheckHandles(GtaProcessId))
            {
                return false;
            }

            var vehicleId = Internal.ReadDword(Internal.GtaBaseAddress, new IntPtr(ADDR_CURRENT_VEHICLE_ID));
            return vehicleId > 0;
        }
        public static double GetPlayerFacingAngle()
        {
            if (!Internal.CheckHandles(GtaProcessId))
            {
                return 0;
            }

            var pedStructAddress = Internal.ReadDword(Internal.GtaBaseAddress, new IntPtr(ADDR_CPED_PTR));
            var facingAngle = Internal.ReadFloat(Internal.GtaBaseAddress, IntPtr.Add(new IntPtr(pedStructAddress), PED_FACING_ANGLE_OFFSET));
            
            return (180 / Math.PI) * facingAngle;
        }

        public static void AddMessageToChat(string text)
        {
            if (!Internal.CheckHandles())
            {
                return;
            }

            var dwFunc = IntPtr.Add(Internal.SampDllBaseAddress, FUNC_SAMP_ADDTOCHATWND[Internal.SampVersion]);
            var dwChatInfo = new IntPtr(Internal.ReadDword(Internal.GtaBaseAddress,
                IntPtr.Add(Internal.SampDllBaseAddress, ADDR_SAMP_CHATMSG_PTR[Internal.SampVersion])));

            Internal.CallWithParams(Internal.GtaBaseAddress,
                dwFunc,
                new[] {
                    new Parameter("p", dwChatInfo),
                    new Parameter("s", text),
                });
        }

        public static void SendChat(string text)
        {
            if (!Internal.CheckHandles())
            {
                return;
            }

            if (IsInChatOrDialog())
            {
                return;
            }

            var dwFunc = text.StartsWith("/")
                ? IntPtr.Add(Internal.SampDllBaseAddress, FUNC_SAMP_SENDCMD[Internal.SampVersion])
                : IntPtr.Add(Internal.SampDllBaseAddress, FUNC_SAMP_SENDSAY[Internal.SampVersion]);

            Internal.CallWithParams(Internal.GtaBaseAddress, dwFunc, new[] { new Parameter("s", text) }, false);
        }
        public static bool IsInChatOrDialog()
        {
            var dwPtr = IntPtr.Add(Internal.SampDllBaseAddress, ADDR_SAMP_INCHAT_PTR[Internal.SampVersion]);

            var dwAddress = IntPtr.Add(new IntPtr(Internal.ReadDword(Internal.GtaBaseAddress, dwPtr)),
                ADDR_SAMP_INCHAT_PTR_OFFSET[Internal.SampVersion]);

            var dwInChat = Internal.ReadDword(Internal.GtaBaseAddress, dwAddress);

            return dwInChat > 0;
        }

        private static bool Intersects(Coordinates point, Coordinates zoneMin, Coordinates zoneMax)
        {
            return point.X >= zoneMin.X && 
                   point.Y >= zoneMin.Y && 
                   point.Z >= zoneMin.Z && 
                   point.X <= zoneMax.X &&
                   point.Y <= zoneMax.Y && 
                   point.Z <= zoneMax.Z;
        }

        private static void InitializeZonesAndCities()
        {
            if (_zones != null && _cities != null) return;
            _zones = new List<Zone>(300);
            _cities = new List<Zone>(10);

            AddCity("Las Venturas", 685.0, 476.093, -500.0, 3000.0, 3000.0, 500.0);
            AddCity("San Fierro", -3000.0, -742.306, -500.0, -1270.53, 1530.24, 500.0);
            AddCity("San Fierro", -1270.53, -402.481, -500.0, -1038.45, 832.495, 500.0);
            AddCity("San Fierro", -1038.45, -145.539, -500.0, -897.546, 376.632, 500.0);
            AddCity("Los Santos", 480.0, -3000.0, -500.0, 3000.0, -850.0, 500.0);
            AddCity("Los Santos", 80.0, -2101.61, -500.0, 1075.0, -1239.61, 500.0);
            AddCity("Tierra Robada", -1213.91, 596.349, -242.99, -480.539, 1659.68, 900.0);
            AddCity("Red County", -1213.91, -768.027, -242.99, 2997.06, 596.349, 900.0);
            AddCity("Flint County", -1213.91, -2892.97, -242.99, 44.6147, -768.027, 900.0);

            AddCity("Whetstone", -2997.47, -2892.97, -242.99, -1213.91, -1115.58, 900.0);
            AddZone("Avispa Country Club", -2667.810, -302.135, -28.831, -2646.400, -262.320, 71.169);
            AddZone("Easter Bay Airport", -1315.420, -405.388, 15.406, -1264.400, -209.543, 25.406);
            AddZone("Avispa Country Club", -2550.040, -355.493, 0.000, -2470.040, -318.493, 39.700);
            AddZone("Easter Bay Airport", -1490.330, -209.543, 15.406, -1264.400, -148.388, 25.406);
            AddZone("Garcia", -2395.140, -222.589, -5.3, -2354.090, -204.792, 200.000);
            AddZone("Shady Cabin", -1632.830, -2263.440, -3.0, -1601.330, -2231.790, 200.000);
            AddZone("East Los Santos", 2381.680, -1494.030, -89.084, 2421.030, -1454.350, 110.916);
            AddZone("LVA Freight Depot", 1236.630, 1163.410, -89.084, 1277.050, 1203.280, 110.916);
            AddZone("Blackfield Intersection", 1277.050, 1044.690, -89.084, 1315.350, 1087.630, 110.916);
            AddZone("Avispa Country Club", -2470.040, -355.493, 0.000, -2270.040, -318.493, 46.100);
            AddZone("Temple", 1252.330, -926.999, -89.084, 1357.000, -910.170, 110.916);
            AddZone("Unity Station", 1692.620, -1971.800, -20.492, 1812.620, -1932.800, 79.508);
            AddZone("LVA Freight Depot", 1315.350, 1044.690, -89.084, 1375.600, 1087.630, 110.916);
            AddZone("Los Flores", 2581.730, -1454.350, -89.084, 2632.830, -1393.420, 110.916);
            AddZone("Starfish Casino", 2437.390, 1858.100, -39.084, 2495.090, 1970.850, 60.916);
            AddZone("Easter Bay Chemicals", -1132.820, -787.391, 0.000, -956.476, -768.027, 200.000);
            AddZone("Downtown Los Santos", 1370.850, -1170.870, -89.084, 1463.900, -1130.850, 110.916);
            AddZone("Esplanade East", -1620.300, 1176.520, -4.5, -1580.010, 1274.260, 200.000);
            AddZone("Market Station", 787.461, -1410.930, -34.126, 866.009, -1310.210, 65.874);
            AddZone("Linden Station", 2811.250, 1229.590, -39.594, 2861.250, 1407.590, 60.406);
            AddZone("Montgomery Intersection", 1582.440, 347.457, 0.000, 1664.620, 401.750, 200.000);
            AddZone("Frederick Bridge", 2759.250, 296.501, 0.000, 2774.250, 594.757, 200.000);
            AddZone("Yellow Bell Station", 1377.480, 2600.430, -21.926, 1492.450, 2687.360, 78.074);
            AddZone("Downtown Los Santos", 1507.510, -1385.210, 110.916, 1582.550, -1325.310, 335.916);
            AddZone("Jefferson", 2185.330, -1210.740, -89.084, 2281.450, -1154.590, 110.916);
            AddZone("Mulholland", 1318.130, -910.170, -89.084, 1357.000, -768.027, 110.916);
            AddZone("Avispa Country Club", -2361.510, -417.199, 0.000, -2270.040, -355.493, 200.000);
            AddZone("Jefferson", 1996.910, -1449.670, -89.084, 2056.860, -1350.720, 110.916);
            AddZone("Julius Thruway West", 1236.630, 2142.860, -89.084, 1297.470, 2243.230, 110.916);
            AddZone("Jefferson", 2124.660, -1494.030, -89.084, 2266.210, -1449.670, 110.916);
            AddZone("Julius Thruway North", 1848.400, 2478.490, -89.084, 1938.800, 2553.490, 110.916);
            AddZone("Rodeo", 422.680, -1570.200, -89.084, 466.223, -1406.050, 110.916);
            AddZone("Cranberry Station", -2007.830, 56.306, 0.000, -1922.000, 224.782, 100.000);
            AddZone("Downtown Los Santos", 1391.050, -1026.330, -89.084, 1463.900, -926.999, 110.916);
            AddZone("Redsands West", 1704.590, 2243.230, -89.084, 1777.390, 2342.830, 110.916);
            AddZone("Little Mexico", 1758.900, -1722.260, -89.084, 1812.620, -1577.590, 110.916);
            AddZone("Blackfield Intersection", 1375.600, 823.228, -89.084, 1457.390, 919.447, 110.916);
            AddZone("Los Santos International", 1974.630, -2394.330, -39.084, 2089.000, -2256.590, 60.916);
            AddZone("Beacon Hill", -399.633, -1075.520, -1.489, -319.033, -977.516, 198.511);
            AddZone("Rodeo", 334.503, -1501.950, -89.084, 422.680, -1406.050, 110.916);
            AddZone("Richman", 225.165, -1369.620, -89.084, 334.503, -1292.070, 110.916);
            AddZone("Downtown Los Santos", 1724.760, -1250.900, -89.084, 1812.620, -1150.870, 110.916);
            AddZone("The Strip", 2027.400, 1703.230, -89.084, 2137.400, 1783.230, 110.916);
            AddZone("Downtown Los Santos", 1378.330, -1130.850, -89.084, 1463.900, -1026.330, 110.916);
            AddZone("Blackfield Intersection", 1197.390, 1044.690, -89.084, 1277.050, 1163.390, 110.916);
            AddZone("Conference Center", 1073.220, -1842.270, -89.084, 1323.900, -1804.210, 110.916);
            AddZone("Montgomery", 1451.400, 347.457, -6.1, 1582.440, 420.802, 200.000);
            AddZone("Foster Valley", -2270.040, -430.276, -1.2, -2178.690, -324.114, 200.000);
            AddZone("Blackfield Chapel", 1325.600, 596.349, -89.084, 1375.600, 795.010, 110.916);
            AddZone("Los Santos International", 2051.630, -2597.260, -39.084, 2152.450, -2394.330, 60.916);
            AddZone("Mulholland", 1096.470, -910.170, -89.084, 1169.130, -768.027, 110.916);
            AddZone("Yellow Bell Gol Course", 1457.460, 2723.230, -89.084, 1534.560, 2863.230, 110.916);
            AddZone("The Strip", 2027.400, 1783.230, -89.084, 2162.390, 1863.230, 110.916);
            AddZone("Jefferson", 2056.860, -1210.740, -89.084, 2185.330, -1126.320, 110.916);
            AddZone("Mulholland", 952.604, -937.184, -89.084, 1096.470, -860.619, 110.916);
            AddZone("Aldea Malvada", -1372.140, 2498.520, 0.000, -1277.590, 2615.350, 200.000);
            AddZone("Las Colinas", 2126.860, -1126.320, -89.084, 2185.330, -934.489, 110.916);
            AddZone("Las Colinas", 1994.330, -1100.820, -89.084, 2056.860, -920.815, 110.916);
            AddZone("Richman", 647.557, -954.662, -89.084, 768.694, -860.619, 110.916);
            AddZone("LVA Freight Depot", 1277.050, 1087.630, -89.084, 1375.600, 1203.280, 110.916);
            AddZone("Julius Thruway North", 1377.390, 2433.230, -89.084, 1534.560, 2507.230, 110.916);
            AddZone("Willowfield", 2201.820, -2095.000, -89.084, 2324.000, -1989.900, 110.916);
            AddZone("Julius Thruway North", 1704.590, 2342.830, -89.084, 1848.400, 2433.230, 110.916);
            AddZone("Temple", 1252.330, -1130.850, -89.084, 1378.330, -1026.330, 110.916);
            AddZone("Little Mexico", 1701.900, -1842.270, -89.084, 1812.620, -1722.260, 110.916);
            AddZone("Queens", -2411.220, 373.539, 0.000, -2253.540, 458.411, 200.000);
            AddZone("Las Venturas Airport", 1515.810, 1586.400, -12.500, 1729.950, 1714.560, 87.500);
            AddZone("Richman", 225.165, -1292.070, -89.084, 466.223, -1235.070, 110.916);
            AddZone("Temple", 1252.330, -1026.330, -89.084, 1391.050, -926.999, 110.916);
            AddZone("East Los Santos", 2266.260, -1494.030, -89.084, 2381.680, -1372.040, 110.916);
            AddZone("Julius Thruway East", 2623.180, 943.235, -89.084, 2749.900, 1055.960, 110.916);
            AddZone("Willowfield", 2541.700, -1941.400, -89.084, 2703.580, -1852.870, 110.916);
            AddZone("Las Colinas", 2056.860, -1126.320, -89.084, 2126.860, -920.815, 110.916);
            AddZone("Julius Thruway East", 2625.160, 2202.760, -89.084, 2685.160, 2442.550, 110.916);
            AddZone("Rodeo", 225.165, -1501.950, -89.084, 334.503, -1369.620, 110.916);
            AddZone("Las Brujas", -365.167, 2123.010, -3.0, -208.570, 2217.680, 200.000);
            AddZone("Julius Thruway East", 2536.430, 2442.550, -89.084, 2685.160, 2542.550, 110.916);
            AddZone("Rodeo", 334.503, -1406.050, -89.084, 466.223, -1292.070, 110.916);
            AddZone("Vinewood", 647.557, -1227.280, -89.084, 787.461, -1118.280, 110.916);
            AddZone("Rodeo", 422.680, -1684.650, -89.084, 558.099, -1570.200, 110.916);
            AddZone("Julius Thruway North", 2498.210, 2542.550, -89.084, 2685.160, 2626.550, 110.916);
            AddZone("Downtown Los Santos", 1724.760, -1430.870, -89.084, 1812.620, -1250.900, 110.916);
            AddZone("Rodeo", 225.165, -1684.650, -89.084, 312.803, -1501.950, 110.916);
            AddZone("Jefferson", 2056.860, -1449.670, -89.084, 2266.210, -1372.040, 110.916);
            AddZone("Hampton Barns", 603.035, 264.312, 0.000, 761.994, 366.572, 200.000);
            AddZone("Temple", 1096.470, -1130.840, -89.084, 1252.330, -1026.330, 110.916);
            AddZone("Kincaid Bridge", -1087.930, 855.370, -89.084, -961.950, 986.281, 110.916);
            AddZone("Verona Beach", 1046.150, -1722.260, -89.084, 1161.520, -1577.590, 110.916);
            AddZone("Commerce", 1323.900, -1722.260, -89.084, 1440.900, -1577.590, 110.916);
            AddZone("Mulholland", 1357.000, -926.999, -89.084, 1463.900, -768.027, 110.916);
            AddZone("Rodeo", 466.223, -1570.200, -89.084, 558.099, -1385.070, 110.916);
            AddZone("Mulholland", 911.802, -860.619, -89.084, 1096.470, -768.027, 110.916);
            AddZone("Mulholland", 768.694, -954.662, -89.084, 952.604, -860.619, 110.916);
            AddZone("Julius Thruway South", 2377.390, 788.894, -89.084, 2537.390, 897.901, 110.916);
            AddZone("Idlewood", 1812.620, -1852.870, -89.084, 1971.660, -1742.310, 110.916);
            AddZone("Ocean Docks", 2089.000, -2394.330, -89.084, 2201.820, -2235.840, 110.916);
            AddZone("Commerce", 1370.850, -1577.590, -89.084, 1463.900, -1384.950, 110.916);
            AddZone("Julius Thruway North", 2121.400, 2508.230, -89.084, 2237.400, 2663.170, 110.916);
            AddZone("Temple", 1096.470, -1026.330, -89.084, 1252.330, -910.170, 110.916);
            AddZone("Glen Park", 1812.620, -1449.670, -89.084, 1996.910, -1350.720, 110.916);
            AddZone("Easter Bay Airport", -1242.980, -50.096, 0.000, -1213.910, 578.396, 200.000);
            AddZone("Martin Bridge", -222.179, 293.324, 0.000, -122.126, 476.465, 200.000);
            AddZone("The Strip", 2106.700, 1863.230, -89.084, 2162.390, 2202.760, 110.916);
            AddZone("Willowfield", 2541.700, -2059.230, -89.084, 2703.580, -1941.400, 110.916);
            AddZone("Marina", 807.922, -1577.590, -89.084, 926.922, -1416.250, 110.916);
            AddZone("Las Venturas Airport", 1457.370, 1143.210, -89.084, 1777.400, 1203.280, 110.916);
            AddZone("Idlewood", 1812.620, -1742.310, -89.084, 1951.660, -1602.310, 110.916);
            AddZone("Esplanade East", -1580.010, 1025.980, -6.1, -1499.890, 1274.260, 200.000);
            AddZone("Downtown Los Santos", 1370.850, -1384.950, -89.084, 1463.900, -1170.870, 110.916);
            AddZone("The Mako Span", 1664.620, 401.750, 0.000, 1785.140, 567.203, 200.000);
            AddZone("Rodeo", 312.803, -1684.650, -89.084, 422.680, -1501.950, 110.916);
            AddZone("Pershing Square", 1440.900, -1722.260, -89.084, 1583.500, -1577.590, 110.916);
            AddZone("Mulholland", 687.802, -860.619, -89.084, 911.802, -768.027, 110.916);
            AddZone("Gant Bridge", -2741.070, 1490.470, -6.1, -2616.400, 1659.680, 200.000);
            AddZone("Las Colinas", 2185.330, -1154.590, -89.084, 2281.450, -934.489, 110.916);
            AddZone("Mulholland", 1169.130, -910.170, -89.084, 1318.130, -768.027, 110.916);
            AddZone("Julius Thruway North", 1938.800, 2508.230, -89.084, 2121.400, 2624.230, 110.916);
            AddZone("Commerce", 1667.960, -1577.590, -89.084, 1812.620, -1430.870, 110.916);
            AddZone("Rodeo", 72.648, -1544.170, -89.084, 225.165, -1404.970, 110.916);
            AddZone("Roca Escalante", 2536.430, 2202.760, -89.084, 2625.160, 2442.550, 110.916);
            AddZone("Rodeo", 72.648, -1684.650, -89.084, 225.165, -1544.170, 110.916);
            AddZone("Market", 952.663, -1310.210, -89.084, 1072.660, -1130.850, 110.916);
            AddZone("Las Colinas", 2632.740, -1135.040, -89.084, 2747.740, -945.035, 110.916);
            AddZone("Mulholland", 861.085, -674.885, -89.084, 1156.550, -600.896, 110.916);
            AddZone("King's", -2253.540, 373.539, -9.1, -1993.280, 458.411, 200.000);
            AddZone("Redsands East", 1848.400, 2342.830, -89.084, 2011.940, 2478.490, 110.916);
            AddZone("Downtown", -1580.010, 744.267, -6.1, -1499.890, 1025.980, 200.000);
            AddZone("Conference Center", 1046.150, -1804.210, -89.084, 1323.900, -1722.260, 110.916);
            AddZone("Richman", 647.557, -1118.280, -89.084, 787.461, -954.662, 110.916);
            AddZone("Ocean Flats", -2994.490, 277.411, -9.1, -2867.850, 458.411, 200.000);
            AddZone("Greenglass College", 964.391, 930.890, -89.084, 1166.530, 1044.690, 110.916);
            AddZone("Glen Park", 1812.620, -1100.820, -89.084, 1994.330, -973.380, 110.916);
            AddZone("LVA Freight Depot", 1375.600, 919.447, -89.084, 1457.370, 1203.280, 110.916);
            AddZone("Regular Tom", -405.770, 1712.860, -3.0, -276.719, 1892.750, 200.000);
            AddZone("Verona Beach", 1161.520, -1722.260, -89.084, 1323.900, -1577.590, 110.916);
            AddZone("East Los Santos", 2281.450, -1372.040, -89.084, 2381.680, -1135.040, 110.916);
            AddZone("Caligula's Palace", 2137.400, 1703.230, -89.084, 2437.390, 1783.230, 110.916);
            AddZone("Idlewood", 1951.660, -1742.310, -89.084, 2124.660, -1602.310, 110.916);
            AddZone("Pilgrim", 2624.400, 1383.230, -89.084, 2685.160, 1783.230, 110.916);
            AddZone("Idlewood", 2124.660, -1742.310, -89.084, 2222.560, -1494.030, 110.916);
            AddZone("Queens", -2533.040, 458.411, 0.000, -2329.310, 578.396, 200.000);
            AddZone("Downtown", -1871.720, 1176.420, -4.5, -1620.300, 1274.260, 200.000);
            AddZone("Commerce", 1583.500, -1722.260, -89.084, 1758.900, -1577.590, 110.916);
            AddZone("East Los Santos", 2381.680, -1454.350, -89.084, 2462.130, -1135.040, 110.916);
            AddZone("Marina", 647.712, -1577.590, -89.084, 807.922, -1416.250, 110.916);
            AddZone("Richman", 72.648, -1404.970, -89.084, 225.165, -1235.070, 110.916);
            AddZone("Vinewood", 647.712, -1416.250, -89.084, 787.461, -1227.280, 110.916);
            AddZone("East Los Santos", 2222.560, -1628.530, -89.084, 2421.030, -1494.030, 110.916);
            AddZone("Rodeo", 558.099, -1684.650, -89.084, 647.522, -1384.930, 110.916);
            AddZone("Easter Tunnel", -1709.710, -833.034, -1.5, -1446.010, -730.118, 200.000);
            AddZone("Rodeo", 466.223, -1385.070, -89.084, 647.522, -1235.070, 110.916);
            AddZone("Redsands East", 1817.390, 2202.760, -89.084, 2011.940, 2342.830, 110.916);
            AddZone("The Clown's Pocket", 2162.390, 1783.230, -89.084, 2437.390, 1883.230, 110.916);
            AddZone("Idlewood", 1971.660, -1852.870, -89.084, 2222.560, -1742.310, 110.916);
            AddZone("Montgomery Intersection", 1546.650, 208.164, 0.000, 1745.830, 347.457, 200.000);
            AddZone("Willowfield", 2089.000, -2235.840, -89.084, 2201.820, -1989.900, 110.916);
            AddZone("Temple", 952.663, -1130.840, -89.084, 1096.470, -937.184, 110.916);
            AddZone("Prickle Pine", 1848.400, 2553.490, -89.084, 1938.800, 2863.230, 110.916);
            AddZone("Los Santos International", 1400.970, -2669.260, -39.084, 2189.820, -2597.260, 60.916);
            AddZone("Garver Bridge", -1213.910, 950.022, -89.084, -1087.930, 1178.930, 110.916);
            AddZone("Garver Bridge", -1339.890, 828.129, -89.084, -1213.910, 1057.040, 110.916);
            AddZone("Kincaid Bridge", -1339.890, 599.218, -89.084, -1213.910, 828.129, 110.916);
            AddZone("Kincaid Bridge", -1213.910, 721.111, -89.084, -1087.930, 950.022, 110.916);
            AddZone("Verona Beach", 930.221, -2006.780, -89.084, 1073.220, -1804.210, 110.916);
            AddZone("Verdant Bluffs", 1073.220, -2006.780, -89.084, 1249.620, -1842.270, 110.916);
            AddZone("Vinewood", 787.461, -1130.840, -89.084, 952.604, -954.662, 110.916);
            AddZone("Vinewood", 787.461, -1310.210, -89.084, 952.663, -1130.840, 110.916);
            AddZone("Commerce", 1463.900, -1577.590, -89.084, 1667.960, -1430.870, 110.916);
            AddZone("Market", 787.461, -1416.250, -89.084, 1072.660, -1310.210, 110.916);
            AddZone("Rockshore West", 2377.390, 596.349, -89.084, 2537.390, 788.894, 110.916);
            AddZone("Julius Thruway North", 2237.400, 2542.550, -89.084, 2498.210, 2663.170, 110.916);
            AddZone("East Beach", 2632.830, -1668.130, -89.084, 2747.740, -1393.420, 110.916);
            AddZone("Fallow Bridge", 434.341, 366.572, 0.000, 603.035, 555.680, 200.000);
            AddZone("Willowfield", 2089.000, -1989.900, -89.084, 2324.000, -1852.870, 110.916);
            AddZone("Chinatown", -2274.170, 578.396, -7.6, -2078.670, 744.170, 200.000);
            AddZone("El Castillo del Diablo", -208.570, 2337.180, 0.000, 8.430, 2487.180, 200.000);
            AddZone("Ocean Docks", 2324.000, -2145.100, -89.084, 2703.580, -2059.230, 110.916);
            AddZone("Easter Bay Chemicals", -1132.820, -768.027, 0.000, -956.476, -578.118, 200.000);
            AddZone("The Visage", 1817.390, 1703.230, -89.084, 2027.400, 1863.230, 110.916);
            AddZone("Ocean Flats", -2994.490, -430.276, -1.2, -2831.890, -222.589, 200.000);
            AddZone("Richman", 321.356, -860.619, -89.084, 687.802, -768.027, 110.916);
            AddZone("Green Palms", 176.581, 1305.450, -3.0, 338.658, 1520.720, 200.000);
            AddZone("Richman", 321.356, -768.027, -89.084, 700.794, -674.885, 110.916);
            AddZone("Starfish Casino", 2162.390, 1883.230, -89.084, 2437.390, 2012.180, 110.916);
            AddZone("East Beach", 2747.740, -1668.130, -89.084, 2959.350, -1498.620, 110.916);
            AddZone("Jefferson", 2056.860, -1372.040, -89.084, 2281.450, -1210.740, 110.916);
            AddZone("Downtown Los Santos", 1463.900, -1290.870, -89.084, 1724.760, -1150.870, 110.916);
            AddZone("Downtown Los Santos", 1463.900, -1430.870, -89.084, 1724.760, -1290.870, 110.916);
            AddZone("Garver Bridge", -1499.890, 696.442, -179.615, -1339.890, 925.353, 20.385);
            AddZone("Julius Thruway South", 1457.390, 823.228, -89.084, 2377.390, 863.229, 110.916);
            AddZone("East Los Santos", 2421.030, -1628.530, -89.084, 2632.830, -1454.350, 110.916);
            AddZone("Greenglass College", 964.391, 1044.690, -89.084, 1197.390, 1203.220, 110.916);
            AddZone("Las Colinas", 2747.740, -1120.040, -89.084, 2959.350, -945.035, 110.916);
            AddZone("Mulholland", 737.573, -768.027, -89.084, 1142.290, -674.885, 110.916);
            AddZone("Ocean Docks", 2201.820, -2730.880, -89.084, 2324.000, -2418.330, 110.916);
            AddZone("East Los Santos", 2462.130, -1454.350, -89.084, 2581.730, -1135.040, 110.916);
            AddZone("Ganton", 2222.560, -1722.330, -89.084, 2632.830, -1628.530, 110.916);
            AddZone("Avispa Country Club", -2831.890, -430.276, -6.1, -2646.400, -222.589, 200.000);
            AddZone("Willowfield", 1970.620, -2179.250, -89.084, 2089.000, -1852.870, 110.916);
            AddZone("Esplanade North", -1982.320, 1274.260, -4.5, -1524.240, 1358.900, 200.000);
            AddZone("The High Roller", 1817.390, 1283.230, -89.084, 2027.390, 1469.230, 110.916);
            AddZone("Ocean Docks", 2201.820, -2418.330, -89.084, 2324.000, -2095.000, 110.916);
            AddZone("Last Dime Motel", 1823.080, 596.349, -89.084, 1997.220, 823.228, 110.916);
            AddZone("Bayside Marina", -2353.170, 2275.790, 0.000, -2153.170, 2475.790, 200.000);
            AddZone("King's", -2329.310, 458.411, -7.6, -1993.280, 578.396, 200.000);
            AddZone("El Corona", 1692.620, -2179.250, -89.084, 1812.620, -1842.270, 110.916);
            AddZone("Blackfield Chapel", 1375.600, 596.349, -89.084, 1558.090, 823.228, 110.916);
            AddZone("The Pink Swan", 1817.390, 1083.230, -89.084, 2027.390, 1283.230, 110.916);
            AddZone("Julius Thruway West", 1197.390, 1163.390, -89.084, 1236.630, 2243.230, 110.916);
            AddZone("Los Flores", 2581.730, -1393.420, -89.084, 2747.740, -1135.040, 110.916);
            AddZone("The Visage", 1817.390, 1863.230, -89.084, 2106.700, 2011.830, 110.916);
            AddZone("Prickle Pine", 1938.800, 2624.230, -89.084, 2121.400, 2861.550, 110.916);
            AddZone("Verona Beach", 851.449, -1804.210, -89.084, 1046.150, -1577.590, 110.916);
            AddZone("Robada Intersection", -1119.010, 1178.930, -89.084, -862.025, 1351.450, 110.916);
            AddZone("Linden Side", 2749.900, 943.235, -89.084, 2923.390, 1198.990, 110.916);
            AddZone("Ocean Docks", 2703.580, -2302.330, -89.084, 2959.350, -2126.900, 110.916);
            AddZone("Willowfield", 2324.000, -2059.230, -89.084, 2541.700, -1852.870, 110.916);
            AddZone("King's", -2411.220, 265.243, -9.1, -1993.280, 373.539, 200.000);
            AddZone("Commerce", 1323.900, -1842.270, -89.084, 1701.900, -1722.260, 110.916);
            AddZone("Mulholland", 1269.130, -768.027, -89.084, 1414.070, -452.425, 110.916);
            AddZone("Marina", 647.712, -1804.210, -89.084, 851.449, -1577.590, 110.916);
            AddZone("Battery Point", -2741.070, 1268.410, -4.5, -2533.040, 1490.470, 200.000);
            AddZone("The Four Dragons Casino", 1817.390, 863.232, -89.084, 2027.390, 1083.230, 110.916);
            AddZone("Blackfield", 964.391, 1203.220, -89.084, 1197.390, 1403.220, 110.916);
            AddZone("Julius Thruway North", 1534.560, 2433.230, -89.084, 1848.400, 2583.230, 110.916);
            AddZone("Yellow Bell Gol Course", 1117.400, 2723.230, -89.084, 1457.460, 2863.230, 110.916);
            AddZone("Idlewood", 1812.620, -1602.310, -89.084, 2124.660, -1449.670, 110.916);
            AddZone("Redsands West", 1297.470, 2142.860, -89.084, 1777.390, 2243.230, 110.916);
            AddZone("Doherty", -2270.040, -324.114, -1.2, -1794.920, -222.589, 200.000);
            AddZone("Hilltop Farm", 967.383, -450.390, -3.0, 1176.780, -217.900, 200.000);
            AddZone("Las Barrancas", -926.130, 1398.730, -3.0, -719.234, 1634.690, 200.000);
            AddZone("Pirates in Men's Pants", 1817.390, 1469.230, -89.084, 2027.400, 1703.230, 110.916);
            AddZone("City Hall", -2867.850, 277.411, -9.1, -2593.440, 458.411, 200.000);
            AddZone("Avispa Country Club", -2646.400, -355.493, 0.000, -2270.040, -222.589, 200.000);
            AddZone("The Strip", 2027.400, 863.229, -89.084, 2087.390, 1703.230, 110.916);
            AddZone("Hashbury", -2593.440, -222.589, -1.0, -2411.220, 54.722, 200.000);
            AddZone("Los Santos International", 1852.000, -2394.330, -89.084, 2089.000, -2179.250, 110.916);
            AddZone("Whitewood Estates", 1098.310, 1726.220, -89.084, 1197.390, 2243.230, 110.916);
            AddZone("Sherman Reservoir", -789.737, 1659.680, -89.084, -599.505, 1929.410, 110.916);
            AddZone("El Corona", 1812.620, -2179.250, -89.084, 1970.620, -1852.870, 110.916);
            AddZone("Downtown", -1700.010, 744.267, -6.1, -1580.010, 1176.520, 200.000);
            AddZone("Foster Valley", -2178.690, -1250.970, 0.000, -1794.920, -1115.580, 200.000);
            AddZone("Las Payasadas", -354.332, 2580.360, 2.0, -133.625, 2816.820, 200.000);
            AddZone("Valle Ocultado", -936.668, 2611.440, 2.0, -715.961, 2847.900, 200.000);
            AddZone("Blackfield Intersection", 1166.530, 795.010, -89.084, 1375.600, 1044.690, 110.916);
            AddZone("Ganton", 2222.560, -1852.870, -89.084, 2632.830, -1722.330, 110.916);
            AddZone("Easter Bay Airport", -1213.910, -730.118, 0.000, -1132.820, -50.096, 200.000);
            AddZone("Redsands East", 1817.390, 2011.830, -89.084, 2106.700, 2202.760, 110.916);
            AddZone("Esplanade East", -1499.890, 578.396, -79.615, -1339.890, 1274.260, 20.385);
            AddZone("Caligula's Palace", 2087.390, 1543.230, -89.084, 2437.390, 1703.230, 110.916);
            AddZone("Royal Casino", 2087.390, 1383.230, -89.084, 2437.390, 1543.230, 110.916);
            AddZone("Richman", 72.648, -1235.070, -89.084, 321.356, -1008.150, 110.916);
            AddZone("Starfish Casino", 2437.390, 1783.230, -89.084, 2685.160, 2012.180, 110.916);
            AddZone("Mulholland", 1281.130, -452.425, -89.084, 1641.130, -290.913, 110.916);
            AddZone("Downtown", -1982.320, 744.170, -6.1, -1871.720, 1274.260, 200.000);
            AddZone("Hankypanky Point", 2576.920, 62.158, 0.000, 2759.250, 385.503, 200.000);
            AddZone("K.A.C.C. Military Fuels", 2498.210, 2626.550, -89.084, 2749.900, 2861.550, 110.916);
            AddZone("Harry Gold Parkway", 1777.390, 863.232, -89.084, 1817.390, 2342.830, 110.916);
            AddZone("Bayside Tunnel", -2290.190, 2548.290, -89.084, -1950.190, 2723.290, 110.916);
            AddZone("Ocean Docks", 2324.000, -2302.330, -89.084, 2703.580, -2145.100, 110.916);
            AddZone("Richman", 321.356, -1044.070, -89.084, 647.557, -860.619, 110.916);
            AddZone("Randolph Industrial Estate", 1558.090, 596.349, -89.084, 1823.080, 823.235, 110.916);
            AddZone("East Beach", 2632.830, -1852.870, -89.084, 2959.350, -1668.130, 110.916);
            AddZone("Flint Water", -314.426, -753.874, -89.084, -106.339, -463.073, 110.916);
            AddZone("Blueberry", 19.607, -404.136, 3.8, 349.607, -220.137, 200.000);
            AddZone("Linden Station", 2749.900, 1198.990, -89.084, 2923.390, 1548.990, 110.916);
            AddZone("Glen Park", 1812.620, -1350.720, -89.084, 2056.860, -1100.820, 110.916);
            AddZone("Downtown", -1993.280, 265.243, -9.1, -1794.920, 578.396, 200.000);
            AddZone("Redsands West", 1377.390, 2243.230, -89.084, 1704.590, 2433.230, 110.916);
            AddZone("Richman", 321.356, -1235.070, -89.084, 647.522, -1044.070, 110.916);
            AddZone("Gant Bridge", -2741.450, 1659.680, -6.1, -2616.400, 2175.150, 200.000);
            AddZone("Lil' Probe Inn", -90.218, 1286.850, -3.0, 153.859, 1554.120, 200.000);
            AddZone("Flint Intersection", -187.700, -1596.760, -89.084, 17.063, -1276.600, 110.916);
            AddZone("Las Colinas", 2281.450, -1135.040, -89.084, 2632.740, -945.035, 110.916);
            AddZone("Sobell Rail Yards", 2749.900, 1548.990, -89.084, 2923.390, 1937.250, 110.916);
            AddZone("The Emerald Isle", 2011.940, 2202.760, -89.084, 2237.400, 2508.230, 110.916);
            AddZone("El Castillo del Diablo", -208.570, 2123.010, -7.6, 114.033, 2337.180, 200.000);
            AddZone("Santa Flora", -2741.070, 458.411, -7.6, -2533.040, 793.411, 200.000);
            AddZone("Playa del Seville", 2703.580, -2126.900, -89.084, 2959.350, -1852.870, 110.916);
            AddZone("Market", 926.922, -1577.590, -89.084, 1370.850, -1416.250, 110.916);
            AddZone("Queens", -2593.440, 54.722, 0.000, -2411.220, 458.411, 200.000);
            AddZone("Pilson Intersection", 1098.390, 2243.230, -89.084, 1377.390, 2507.230, 110.916);
            AddZone("Spinybed", 2121.400, 2663.170, -89.084, 2498.210, 2861.550, 110.916);
            AddZone("Pilgrim", 2437.390, 1383.230, -89.084, 2624.400, 1783.230, 110.916);
            AddZone("Blackfield", 964.391, 1403.220, -89.084, 1197.390, 1726.220, 110.916);
            AddZone("'The Big Ear'", -410.020, 1403.340, -3.0, -137.969, 1681.230, 200.000);
            AddZone("Dillimore", 580.794, -674.885, -9.5, 861.085, -404.790, 200.000);
            AddZone("El Quebrados", -1645.230, 2498.520, 0.000, -1372.140, 2777.850, 200.000);
            AddZone("Esplanade North", -2533.040, 1358.900, -4.5, -1996.660, 1501.210, 200.000);
            AddZone("Easter Bay Airport", -1499.890, -50.096, -1.0, -1242.980, 249.904, 200.000);
            AddZone("Fisher's Lagoon", 1916.990, -233.323, -100.000, 2131.720, 13.800, 200.000);
            AddZone("Mulholland", 1414.070, -768.027, -89.084, 1667.610, -452.425, 110.916);
            AddZone("East Beach", 2747.740, -1498.620, -89.084, 2959.350, -1120.040, 110.916);
            AddZone("San Andreas Sound", 2450.390, 385.503, -100.000, 2759.250, 562.349, 200.000);
            AddZone("Shady Creeks", -2030.120, -2174.890, -6.1, -1820.640, -1771.660, 200.000);
            AddZone("Market", 1072.660, -1416.250, -89.084, 1370.850, -1130.850, 110.916);
            AddZone("Rockshore West", 1997.220, 596.349, -89.084, 2377.390, 823.228, 110.916);
            AddZone("Prickle Pine", 1534.560, 2583.230, -89.084, 1848.400, 2863.230, 110.916);
            AddZone("Easter Basin", -1794.920, -50.096, -1.04, -1499.890, 249.904, 200.000);
            AddZone("Leafy Hollow", -1166.970, -1856.030, 0.000, -815.624, -1602.070, 200.000);
            AddZone("LVA Freight Depot", 1457.390, 863.229, -89.084, 1777.400, 1143.210, 110.916);
            AddZone("Prickle Pine", 1117.400, 2507.230, -89.084, 1534.560, 2723.230, 110.916);
            AddZone("Blueberry", 104.534, -220.137, 2.3, 349.607, 152.236, 200.000);
            AddZone("El Castillo del Diablo", -464.515, 2217.680, 0.000, -208.570, 2580.360, 200.000);
            AddZone("Downtown", -2078.670, 578.396, -7.6, -1499.890, 744.267, 200.000);
            AddZone("Rockshore East", 2537.390, 676.549, -89.084, 2902.350, 943.235, 110.916);
            AddZone("San Fierro Bay", -2616.400, 1501.210, -3.0, -1996.660, 1659.680, 200.000);
            AddZone("Paradiso", -2741.070, 793.411, -6.1, -2533.040, 1268.410, 200.000);
            AddZone("The Camel's Toe", 2087.390, 1203.230, -89.084, 2640.400, 1383.230, 110.916);
            AddZone("Old Venturas Strip", 2162.390, 2012.180, -89.084, 2685.160, 2202.760, 110.916);
            AddZone("Juniper Hill", -2533.040, 578.396, -7.6, -2274.170, 968.369, 200.000);
            AddZone("Juniper Hollow", -2533.040, 968.369, -6.1, -2274.170, 1358.900, 200.000);
            AddZone("Roca Escalante", 2237.400, 2202.760, -89.084, 2536.430, 2542.550, 110.916);
            AddZone("Julius Thruway East", 2685.160, 1055.960, -89.084, 2749.900, 2626.550, 110.916);
            AddZone("Verona Beach", 647.712, -2173.290, -89.084, 930.221, -1804.210, 110.916);
            AddZone("Foster Valley", -2178.690, -599.884, -1.2, -1794.920, -324.114, 200.000);
            AddZone("Arco del Oeste", -901.129, 2221.860, 0.000, -592.090, 2571.970, 200.000);
            AddZone("Fallen Tree", -792.254, -698.555, -5.3, -452.404, -380.043, 200.000);
            AddZone("The Farm", -1209.670, -1317.100, 114.981, -908.161, -787.391, 251.981);
            AddZone("The Sherman Dam", -968.772, 1929.410, -3.0, -481.126, 2155.260, 200.000);
            AddZone("Esplanade North", -1996.660, 1358.900, -4.5, -1524.240, 1592.510, 200.000);
            AddZone("Financial", -1871.720, 744.170, -6.1, -1701.300, 1176.420, 300.000);
            AddZone("Garcia", -2411.220, -222.589, -1.14, -2173.040, 265.243, 200.000);
            AddZone("Montgomery", 1119.510, 119.526, -3.0, 1451.400, 493.323, 200.000);
            AddZone("Creek", 2749.900, 1937.250, -89.084, 2921.620, 2669.790, 110.916);
            AddZone("Los Santos International", 1249.620, -2394.330, -89.084, 1852.000, -2179.250, 110.916);
            AddZone("Santa Maria Beach", 72.648, -2173.290, -89.084, 342.648, -1684.650, 110.916);
            AddZone("Mulholland Intersection", 1463.900, -1150.870, -89.084, 1812.620, -768.027, 110.916);
            AddZone("Angel Pine", -2324.940, -2584.290, -6.1, -1964.220, -2212.110, 200.000);
            AddZone("Verdant Meadows", 37.032, 2337.180, -3.0, 435.988, 2677.900, 200.000);
            AddZone("Octane Springs", 338.658, 1228.510, 0.000, 664.308, 1655.050, 200.000);
            AddZone("Come-A-Lot", 2087.390, 943.235, -89.084, 2623.180, 1203.230, 110.916);
            AddZone("Redsands West", 1236.630, 1883.110, -89.084, 1777.390, 2142.860, 110.916);
            AddZone("Santa Maria Beach", 342.648, -2173.290, -89.084, 647.712, -1684.650, 110.916);
            AddZone("Verdant Bluffs", 1249.620, -2179.250, -89.084, 1692.620, -1842.270, 110.916);
            AddZone("Las Venturas Airport", 1236.630, 1203.280, -89.084, 1457.370, 1883.110, 110.916);
            AddZone("Flint Range", -594.191, -1648.550, 0.000, -187.700, -1276.600, 200.000);
            AddZone("Verdant Bluffs", 930.221, -2488.420, -89.084, 1249.620, -2006.780, 110.916);
            AddZone("Palomino Creek", 2160.220, -149.004, 0.000, 2576.920, 228.322, 200.000);
            AddZone("Ocean Docks", 2373.770, -2697.090, -89.084, 2809.220, -2330.460, 110.916);
            AddZone("Easter Bay Airport", -1213.910, -50.096, -4.5, -947.980, 578.396, 200.000);
            AddZone("Whitewood Estates", 883.308, 1726.220, -89.084, 1098.310, 2507.230, 110.916);
            AddZone("Calton Heights", -2274.170, 744.170, -6.1, -1982.320, 1358.900, 200.000);
            AddZone("Easter Basin", -1794.920, 249.904, -9.1, -1242.980, 578.396, 200.000);
            AddZone("Los Santos Inlet", -321.744, -2224.430, -89.084, 44.615, -1724.430, 110.916);
            AddZone("Doherty", -2173.040, -222.589, -1.0, -1794.920, 265.243, 200.000);
            AddZone("Mount Chiliad", -2178.690, -2189.910, -47.917, -2030.120, -1771.660, 576.083);
            AddZone("Fort Carson", -376.233, 826.326, -3.0, 123.717, 1220.440, 200.000);
            AddZone("Foster Valley", -2178.690, -1115.580, 0.000, -1794.920, -599.884, 200.000);
            AddZone("Ocean Flats", -2994.490, -222.589, -1.0, -2593.440, 277.411, 200.000);
            AddZone("Fern Ridge", 508.189, -139.259, 0.000, 1306.660, 119.526, 200.000);
            AddZone("Bayside", -2741.070, 2175.150, 0.000, -2353.170, 2722.790, 200.000);
            AddZone("Las Venturas Airport", 1457.370, 1203.280, -89.084, 1777.390, 1883.110, 110.916);
            AddZone("Blueberry Acres", -319.676, -220.137, 0.000, 104.534, 293.324, 200.000);
            AddZone("Palisades", -2994.490, 458.411, -6.1, -2741.070, 1339.610, 200.000);
            AddZone("North Rock", 2285.370, -768.027, 0.000, 2770.590, -269.740, 200.000);
            AddZone("Hunter Quarry", 337.244, 710.840, -115.239, 860.554, 1031.710, 203.761);
            AddZone("Los Santos International", 1382.730, -2730.880, -89.084, 2201.820, -2394.330, 110.916);
            AddZone("Missionary Hill", -2994.490, -811.276, 0.000, -2178.690, -430.276, 200.000);
            AddZone("San Fierro Bay", -2616.400, 1659.680, -3.0, -1996.660, 2175.150, 200.000);
            AddZone("Restricted Area", -91.586, 1655.050, -50.000, 421.234, 2123.010, 250.000);
            AddZone("Mount Chiliad", -2997.470, -1115.580, -47.917, -2178.690, -971.913, 576.083);
            AddZone("Mount Chiliad", -2178.690, -1771.660, -47.917, -1936.120, -1250.970, 576.083);
            AddZone("Easter Bay Airport", -1794.920, -730.118, -3.0, -1213.910, -50.096, 200.000);
            AddZone("The Panopticon", -947.980, -304.320, -1.1, -319.676, 327.071, 200.000);
            AddZone("Shady Creeks", -1820.640, -2643.680, -8.0, -1226.780, -1771.660, 200.000);
            AddZone("Back o Beyond", -1166.970, -2641.190, 0.000, -321.744, -1856.030, 200.000);
            AddZone("Mount Chiliad", -2994.490, -2189.910, -47.917, -2178.690, -1115.580, 576.083);
            AddZone("Tierra Robada", -1213.910, 596.349, -242.990, -480.539, 1659.680, 900.000);
            AddZone("Flint County", -1213.910, -2892.970, -242.990, 44.615, -768.027, 900.000);
            AddZone("Whetstone", -2997.470, -2892.970, -242.990, -1213.910, -1115.580, 900.000);
            AddZone("Bone County", -480.539, 596.349, -242.990, 869.461, 2993.870, 900.000);
            AddZone("Tierra Robada", -2997.470, 1659.680, -242.990, -480.539, 2993.870, 900.000);
            AddZone("San Fierro", -2997.470, -1115.580, -242.990, -1213.910, 1659.680, 900.000);
            AddZone("Las Venturas", 869.461, 596.349, -242.990, 2997.060, 2993.870, 900.000);
            AddZone("Red County", -1213.910, -768.027, -242.990, 2997.060, 596.349, 900.000);
            AddZone("Los Santos", 44.615, -2892.970, -242.990, 2997.060, -768.027, 900.000);
        }

        private static void AddCity(string cityName, double x1, double y1, double z1, double x2, double y2, double z2)
        {
            _cities.Add(new Zone() { Name = cityName, X1 = x1, X2 = x2, Y1 = y1, Y2 = y2, Z1 = z1, Z2 = z2 });
        }

        private static void AddZone(string zoneName, double x1, double y1, double z1, double x2, double y2, double z2)
        {
            _zones.Add(new Zone() { Name = zoneName, X1 = x1, X2 = x2, Y1 = y1, Y2 = y2, Z1 = z1, Z2 = z2 });
        }
    }
}
