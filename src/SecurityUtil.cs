using System;
using System.Runtime.InteropServices;

namespace HamStartup
{
    /// <summary>Detects whether we are running inside a low-integrity sandbox.</summary>
    public static class SecurityUtil
    {
        private const uint TOKEN_QUERY = 0x0008;
        private const int TokenIntegrityLevel = 25;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass,
            IntPtr tokenInformation, int tokenInformationLength, out int returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        public static int GetIntegrityRid()
        {
            IntPtr token = IntPtr.Zero;
            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out token)) return 0;
                int size;
                GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out size);
                if (size <= 0) return 0;

                buffer = Marshal.AllocHGlobal(size);
                int returned;
                if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, size, out returned)) return 0;

                // TOKEN_MANDATORY_LABEL { SID_AND_ATTRIBUTES { PSID Sid; DWORD Attributes; } }
                IntPtr sid = Marshal.ReadIntPtr(buffer);
                if (sid == IntPtr.Zero) return 0;

                // SID layout: revision(1), subAuthorityCount(1), identifierAuthority(6), subAuthority[0]
                return Marshal.ReadInt32(sid, 8);
            }
            catch
            {
                return 0;
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                if (token != IntPtr.Zero) CloseHandle(token);
            }
        }

        public static bool IsLowIntegrity()
        {
            int rid = GetIntegrityRid();
            return rid > 0 && rid < 0x2000;
        }
    }
}
