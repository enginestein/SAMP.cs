using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace SAMPCS
{
    internal class Internal
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            int dwSize,
            ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            int nSize,
            ref int lpNumberOfBytesWritten);

        [DllImport("kernel32")]
        public static extern IntPtr CreateRemoteThread(
          IntPtr hProcess,
          IntPtr lpThreadAttributes,
          uint dwStackSize,
          IntPtr lpStartAddress, 
          IntPtr lpParameter,
          uint dwCreationFlags,
          out uint lpThreadId
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize,
            uint flNewProtect, out uint lpflOldProtect);

        [Flags]
        public enum FreeType
        {
            Decommit = 0x4000,
            Release = 0x8000,
        }
        [Flags]
        public enum AllocationType
        {
            Commit = 0x1000,
            Reserve = 0x2000,
            Decommit = 0x4000,
            Release = 0x8000,
            Reset = 0x80000,
            Physical = 0x400000,
            TopDown = 0x100000,
            WriteWatch = 0x200000,
            LargePages = 0x20000000
        }

        [Flags]
        public enum MemoryProtection
        {
            Execute = 0x10,
            ExecuteRead = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            NoAccess = 0x01,
            ReadOnly = 0x02,
            ReadWrite = 0x04,
            WriteCopy = 0x08,
            GuardModifierflag = 0x100,
            NoCacheModifierflag = 0x200,
            WriteCombineModifierflag = 0x400
        }

        private const uint PageExecuteReadwrite = 0x40;

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress,
            int dwSize, FreeType dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,
            int dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        private static readonly IntPtr[] ReservedMemoryParameter = new IntPtr[5];

        public static IntPtr PInjectFunc = IntPtr.Zero,
            GtaBaseAddress = IntPtr.Zero,
            ApiReservedMemoryBaseAddress = IntPtr.Zero,
            SampDllBaseAddress = IntPtr.Zero;

        public static int SampVersion = 0;
        static int _dwGtapid = 0;

        [DllImport("psapi.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int EnumProcessModules(IntPtr hProcess, [Out] IntPtr lphModule, uint cb, out uint lpcbNeeded);

        [DllImport("psapi.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, uint nSize);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct InputInfo
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5340, ArraySubType = UnmanagedType.I1)]
            public char[] pad5340;
            public int f5340;
        }

        private static readonly Dictionary<string, int> TypeSizes = new Dictionary<string, int>
        {
            { "Int", 4 },
            { "UInt", 4 },
            { "Char", 1 },
            { "UChar", 1 },
            { "Short", 2 },
            { "UShort", 2 },
        };

        public static void WriteRaw(IntPtr hProcess, IntPtr dwAddress, byte[] pBuffer, int dwLen)
        {
            var lpNumberOfBytesWritten = 0;
            WriteProcessMemory(hProcess, dwAddress, pBuffer, dwLen, ref lpNumberOfBytesWritten);
        }

        public static byte[] ReadMem(IntPtr hProcess, IntPtr dwAddress, int dwLen)
        {
            var dwRead = new byte[dwLen];
            var bytesRead = 0;
            var success = ReadProcessMemory(hProcess, dwAddress, dwRead, dwLen, ref bytesRead);
            if (!success)
            {
                throw new Exception("ReadProcessMemory failed. Check if the process is still accessible.");
            }

            return dwRead;
        }


        public static float ReadFloat(IntPtr hProcess, IntPtr dwAddress)
        {
            var bytes = ReadMem(hProcess, dwAddress, 4);
            return BitConverter.ToSingle(bytes, 0);
        }

        public static int ReadDword(IntPtr hProcess, IntPtr dwAddress)
        {
            var bytes = ReadMem(hProcess, dwAddress, 4);
            return BitConverter.ToInt32(bytes, 0);
        }

        public static void WriteString(IntPtr hProcess, IntPtr dwAddress, string wString)
        {
            var buffer = Encoding.ASCII.GetBytes(wString);
            var bytesWritten = 0;
            WriteProcessMemory(hProcess, dwAddress, buffer, buffer.Length, ref bytesWritten);
        }

        private static void NumPut(int num, ref byte[] arr, int startPos)
        {
            var buff = BitConverter.GetBytes(num);
            for (int i = startPos, j = 0; i < startPos + buff.Length; i++, j++)
            {
                arr[i] = buff[j];
            }
        }

        private static void NumPut(uint num, ref byte[] arr, int startPos)
        {
            var buff = BitConverter.GetBytes(num);
            for (int i = startPos, j = 0; i < startPos + buff.Length; i++, j++)
            {
                arr[i] = buff[j];
            }
        }

        private static void NumPut(char num, ref byte[] arr, int startPos)
        {
            var buff = BitConverter.GetBytes(num);
            for (int i = startPos, j = 0; i < startPos + buff.Length; i++, j++)
            {
                arr[i] = buff[j];
            }
        }

        private static void NumPut(byte num, ref byte[] arr, int startPos)
        {
            arr[startPos] = num;
        }

        private static void NumPut(short num, ref byte[] arr, int startPos)
        {
            var buff = BitConverter.GetBytes(num);
            for (int i = startPos, j = 0; i < startPos + buff.Length; i++, j++)
            {
                arr[i] = buff[j];
            }
        }


        private static void NumPut(ushort num, ref byte[] arr, int startPos)
        {
            var buff = BitConverter.GetBytes(num);
            for (int i = startPos, j = 0; i < startPos + buff.Length; i++, j++)
            {
                arr[i] = buff[j];
            }
        }

        public static void CallWithParams(IntPtr hProcess, IntPtr dwFunc, Parameter[] aParams, bool bCleanupStack = true)
        {
            if (!RefreshMemory())
            {
                return;
            }

            var validParams = 0;
            var paramsLength = aParams.Length;
            var dwLen = paramsLength * 5 + 5 + 1;

            if (bCleanupStack)
                dwLen += 3;

            var injectData = new byte[paramsLength * 5 + 5 + 3 + 1];
            
            var pParamIndex = 0;

            for (var i = aParams.Length - 1; i >= 0; i--)
            {
                if (aParams[i].Name == "") continue;
                IntPtr dwMemAddress;

                switch (aParams[i].Name)
                {
                    case "p":
                        dwMemAddress = (IntPtr)aParams[i].Value;
                        break;
                    case "i":
                        dwMemAddress = new IntPtr((int)aParams[i].Value);
                        break;
                    case "s" when pParamIndex > 2:
                        return;
                    case "s":
                        dwMemAddress = ReservedMemoryParameter[pParamIndex];
                        WriteString(hProcess, dwMemAddress, (string)aParams[i].Value);
                        pParamIndex++;
                        break;
                    default:
                        return;
                }
                NumPut((byte)0x68, ref injectData, validParams * 5);
                NumPut(dwMemAddress.ToInt32(), ref injectData, validParams * 5 + 1);
                validParams++;
            }

            var offset = IntPtr.Subtract(dwFunc, IntPtr.Add(PInjectFunc, validParams * 5 + 5).ToInt32());
            NumPut((byte)0xE8, ref injectData, validParams * 5);
            NumPut(offset.ToInt32(), ref injectData, validParams * 5 + 1);

            if (bCleanupStack)
            {
                NumPut((ushort)0xC483, ref injectData, validParams * 5 + 5);
                NumPut((byte)(validParams * 4), ref injectData, validParams * 5 + 7);

                NumPut((byte)0xC3, ref injectData, validParams * 5 + 8);
            }
            else
            {
                NumPut((byte)0xC3, ref injectData, validParams * 5 + 5);
            }

            WriteRaw(GtaBaseAddress, PInjectFunc, injectData, dwLen);

            var hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0,
                PInjectFunc, IntPtr.Zero, 0, out _);

            WaitForSingleObject(hThread, 0xFFFFFFFF);

            CloseHandle(hThread);
        }

        private static bool RefreshSamp()
        {
            SampDllBaseAddress = GetModuleBaseAddress("samp.dll", GtaBaseAddress);
            var versionByte = ReadMem(GtaBaseAddress, SampDllBaseAddress + 0x1036, 1)[0];
            SampVersion = versionByte == 0xD8 ? 1 : (versionByte == 0xA8 ? 2 : (versionByte == 0x78 ? 3 : 0));

            if (SampVersion == 0)
                return false;

            SampVersion -= 1;

            return true;
        }

        private static bool _handleCheckResult;
        private static DateTime _lastHandleCheck = DateTime.MinValue;

        public static bool CheckHandles(int? gtaProcessId = null)
        {
            if (_handleCheckResult && (DateTime.Now - _lastHandleCheck).TotalSeconds < 15)
            {
                return true;
            }

             _handleCheckResult = RefreshGta(gtaProcessId) &&
                RefreshMemory() && 
                RefreshSamp();
            _lastHandleCheck = DateTime.Now;

            return _handleCheckResult;
        }

        private static void ResetPointers()
        {
            GtaBaseAddress = IntPtr.Zero;
            _dwGtapid = 0;
            ApiReservedMemoryBaseAddress = IntPtr.Zero;
            SampDllBaseAddress = IntPtr.Zero;
        }

        private static bool RefreshGta(int? gtaProcessId)
        {
            var newPid = gtaProcessId ?? GetPid("GTA:SA:MP");

            if (newPid < 0)
            {
                if (GtaBaseAddress != IntPtr.Zero)
                {
                    VirtualFreeEx(GtaBaseAddress, ApiReservedMemoryBaseAddress, 0, FreeType.Release);
                }

                ResetPointers();
                return false;
            }

            if (GtaBaseAddress == IntPtr.Zero || _dwGtapid != newPid)
            {
                GtaBaseAddress = OpenProcess(0x1F0FFF, false, newPid);

                _dwGtapid = newPid;
                ApiReservedMemoryBaseAddress = IntPtr.Zero;
            }

            return true;
        }

        private static bool RefreshMemory()
        {
            ApiReservedMemoryBaseAddress = VirtualAllocEx(GtaBaseAddress, IntPtr.Zero, 6144,
                AllocationType.Commit | AllocationType.Reserve,
                MemoryProtection.ExecuteReadWrite);

            for (var i = 0; i < ReservedMemoryParameter.Length; i++)
            {
                ReservedMemoryParameter[i] = ApiReservedMemoryBaseAddress + i * 1024;
            }
            PInjectFunc = ReservedMemoryParameter[ReservedMemoryParameter.Length - 1] + 1024;

            return true;
        }

        private static IntPtr GetModuleBaseAddress(string moduleName, IntPtr hProcess)
        {
            var hMods = new IntPtr[1024];
            var gch = GCHandle.Alloc(hMods, GCHandleType.Pinned); 
            var pModules = gch.AddrOfPinnedObject();
            var uiSize = (uint)(Marshal.SizeOf(typeof(IntPtr)) * (hMods.Length));

            if (EnumProcessModules(hProcess, pModules, uiSize, out var cbNeeded) == 1)
            {
                var uiTotalNumberofModules = (int)(cbNeeded / (Marshal.SizeOf(typeof(IntPtr))));

                for (var i = 0; i < uiTotalNumberofModules; i++)
                {
                    var strbld = new StringBuilder(1024);
                    GetModuleFileNameEx(hProcess, hMods[i], strbld, (uint)(strbld.Capacity));

                    if (System.IO.Path.GetFileName(strbld.ToString()).Equals(moduleName))
                        return hMods[i];
                }
            }

            gch.Free();
            return IntPtr.Zero;
        }

        private static int GetPid(string windowTitle)
        {
            var processes = Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.MainWindowTitle)).ToList();
            foreach (var process in processes)
            {
                var id = process.Id;
                var title = process.MainWindowTitle;

                if (title.Equals(windowTitle))
                    return id;
            }

            return -1;
        }
    }

    class Parameter
    {
        public string Name;
        public object Value;

        public Parameter(string name, object value)
        {
            this.Name = name;
            this.Value = value;
        }
    }
}