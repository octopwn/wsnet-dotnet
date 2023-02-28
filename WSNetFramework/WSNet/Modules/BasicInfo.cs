using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WSNet
{
    class BasicInfo
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule,
            [MarshalAs(UnmanagedType.LPStr)] string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        [DllImport("Secur32.dll", SetLastError = false)]
        private static extern uint LsaGetLogonSessionData(IntPtr luid,
        out IntPtr ppLogonSessionData);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
             ProcessAccessFlags processAccess,
             bool bInheritHandle,
             int processId
        );

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle,
       UInt32 DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        [DllImport("kernel32")]
        private static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32")]
        private static extern void GetNativeSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_INFO
        {
            public ushort processorArchitecture;
            ushort reserved;
            public uint pageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr activeProcessorMask;
            public uint numberOfProcessors;
            public uint processorType;
            public uint allocationGranularity;
            public ushort processorLevel;
            public ushort processorRevision;
        }

        private static uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        private static uint STANDARD_RIGHTS_READ = 0x00020000;
        private static uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private static uint TOKEN_DUPLICATE = 0x0002;
        private static uint TOKEN_IMPERSONATE = 0x0004;
        private static uint TOKEN_QUERY = 0x0008;
        private static uint TOKEN_QUERY_SOURCE = 0x0010;
        private static uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private static uint TOKEN_ADJUST_GROUPS = 0x0040;
        private static uint TOKEN_ADJUST_DEFAULT = 0x0080;
        private static uint TOKEN_ADJUST_SESSIONID = 0x0100;
        private static uint TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);
        private static uint TOKEN_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY |
            TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE |
            TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT |
            TOKEN_ADJUST_SESSIONID);

        public enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId,
            TokenGroupsAndPrivileges,
            TokenSessionReference,
            TokenSandBoxInert,
            TokenAuditPolicy,
            TokenOrigin
        }

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LSA_UNICODE_STRING
        {
            public UInt16 Length;
            public UInt16 MaximumLength;
            public IntPtr buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public UInt32 LowPart;
            public UInt32 HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_LOGON_SESSION_DATA
        {
            public UInt32 Size;
            public LUID LoginID;
            public LSA_UNICODE_STRING Username;
            public LSA_UNICODE_STRING LoginDomain;
            public LSA_UNICODE_STRING AuthenticationPackage;
            public UInt32 LogonType;
            public UInt32 Session;
            public IntPtr PSiD;
            public UInt64 LoginTime;
            public LSA_UNICODE_STRING LogonServer;
            public LSA_UNICODE_STRING DnsDomainName;
            public LSA_UNICODE_STRING Upn;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_STATISTICS
        {
            public LUID TokenId;
            public LUID AuthenticationId;
        }

        public static string getCPUArch()
        {
            string res;
            var si = new SYSTEM_INFO();
            GetSystemInfo(ref si);
            if (si.processorType == 9)
            {
                res = "x64";
            }
            else if (si.processorType == 0)
            {
                res = "x86";
            }
            else
            {
                res = si.processorType.ToString();
            }

            return res;

        }

        public static Tuple<string, string> getLogonServer()
        {
            try
            {
                IntPtr pprocess = OpenProcess(ProcessAccessFlags.QueryInformation, false, Process.GetCurrentProcess().Id);
                IntPtr ptoken;
                if (!OpenProcessToken(pprocess, TOKEN_READ, out ptoken))
                    return null;
                uint tokenInfoLength = 0;
                bool result;
                result = GetTokenInformation(ptoken, TOKEN_INFORMATION_CLASS.TokenStatistics, IntPtr.Zero, tokenInfoLength, out tokenInfoLength);
                IntPtr tokenInfo = Marshal.AllocHGlobal((int)tokenInfoLength);
                result = GetTokenInformation(ptoken, TOKEN_INFORMATION_CLASS.TokenStatistics, tokenInfo, tokenInfoLength, out tokenInfoLength);
                if (result)
                {
                    TOKEN_STATISTICS tstats = (TOKEN_STATISTICS)Marshal.PtrToStructure(tokenInfo, typeof(TOKEN_STATISTICS));
                    IntPtr sessionData;
                    LsaGetLogonSessionData(tokenInfo + 8, out sessionData); //kids please dont try this at home...
                    SECURITY_LOGON_SESSION_DATA logonsess = (SECURITY_LOGON_SESSION_DATA)Marshal.PtrToStructure(sessionData, typeof(SECURITY_LOGON_SESSION_DATA));
                    string domain = Marshal.PtrToStringUni(logonsess.DnsDomainName.buffer).Trim();
                    string logonserver = Marshal.PtrToStringUni(logonsess.LogonServer.buffer).Trim();
                    Tuple<string, string> res = new Tuple<string, string>(domain, logonserver);
                    return res;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static byte[] getinfo()
        {
            string pid = Process.GetCurrentProcess().Id.ToString();
            string username = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            Tuple<string, string> dl = BasicInfo.getLogonServer();
            string domain = dl.Item1;
            string logonserver = dl.Item2;
            string cpuarch = BasicInfo.getCPUArch();
            string hostname = System.Net.Dns.GetHostEntry("").HostName;
            string usersid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;

            CMDInfoReply res = new CMDInfoReply(pid, username, domain, logonserver, cpuarch, hostname, usersid);
            return res.to_bytes();

        }
    }
}
