using System.ComponentModel;
using System.Diagnostics;
using Sentinela.Agent.Interop;

namespace Sentinela.Agent.Workers;

internal static class UserSessionProcess
{
    public static IReadOnlyList<uint> CandidateSessionIds()
    {
        var ids = new List<uint>();
        var console = NativeMethods.WTSGetActiveConsoleSessionId();
        if (console != NativeMethods.INVALID_SESSION_ID && console != 0)
            ids.Add(console);

        if (NativeMethods.WTSEnumerateSessions(NativeMethods.WTS_CURRENT_SERVER_HANDLE, 0, 1, out var ptr, out var count) && ptr != IntPtr.Zero)
        {
            try
            {
                var size = Marshal.SizeOf<NativeMethods.WTS_SESSION_INFO>();
                for (var i = 0; i < count; i++)
                {
                    var info = Marshal.PtrToStructure<NativeMethods.WTS_SESSION_INFO>(ptr + (i * size));
                    if (info.SessionId == 0)
                        continue;
                    if (info.State is NativeMethods.WTS_CONNECTSTATE_CLASS.WTSActive
                        or NativeMethods.WTS_CONNECTSTATE_CLASS.WTSConnected)
                    {
                        if (!ids.Contains(info.SessionId))
                            ids.Add(info.SessionId);
                    }
                }
            }
            finally
            {
                NativeMethods.WTSFreeMemory(ptr);
            }
        }

        return ids;
    }

    public static int? FindInteractiveAgent(int excludePid)
    {
        foreach (var process in Process.GetProcessesByName("Sentinela.Agent"))
        {
            try
            {
                if (process.Id == excludePid || process.SessionId == 0)
                    continue;
                return process.Id;
            }
            catch
            {
                // process exited while enumerating
            }
            finally
            {
                process.Dispose();
            }
        }

        return null;
    }

    public static int StartInteractiveAgent(string exePath, string workingDirectory)
    {
        foreach (var sessionId in CandidateSessionIds())
        {
            if (TryStart(sessionId, exePath, workingDirectory, out var pid))
                return pid;
        }

        throw new Win32Exception(Marshal.GetLastWin32Error() is var err and not 0 ? err : 1008,
            "Nenhuma sessão de usuário disponível para captura de tela.");
    }

    private static bool TryStart(uint sessionId, string exePath, string workingDirectory, out int pid)
    {
        pid = 0;
        if (!NativeMethods.WTSQueryUserToken(sessionId, out var userToken) || userToken == IntPtr.Zero)
            return false;

        var primary = IntPtr.Zero;
        var environment = IntPtr.Zero;
        var processInfo = new NativeMethods.PROCESS_INFORMATION();
        try
        {
            var access = NativeMethods.TOKEN_ASSIGN_PRIMARY
                | NativeMethods.TOKEN_DUPLICATE
                | NativeMethods.TOKEN_QUERY
                | NativeMethods.TOKEN_ADJUST_DEFAULT
                | NativeMethods.TOKEN_ADJUST_SESSIONID;

            if (!NativeMethods.DuplicateTokenEx(
                    userToken,
                    access,
                    IntPtr.Zero,
                    NativeMethods.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    NativeMethods.TOKEN_TYPE.TokenPrimary,
                    out primary)
                || primary == IntPtr.Zero)
            {
                return false;
            }

            NativeMethods.CreateEnvironmentBlock(out environment, primary, false);

            var startup = new NativeMethods.STARTUPINFO
            {
                cb = Marshal.SizeOf<NativeMethods.STARTUPINFO>(),
                lpDesktop = @"winsta0\default",
                dwFlags = NativeMethods.STARTF_USESHOWWINDOW,
                wShowWindow = (short)NativeMethods.SW_HIDE
            };

            var command = new StringBuilder($"\"{exePath}\" --interactive");
            var flags = NativeMethods.CREATE_UNICODE_ENVIRONMENT | NativeMethods.CREATE_NO_WINDOW;
            if (!NativeMethods.CreateProcessAsUserW(
                    primary,
                    exePath,
                    command,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    flags,
                    environment,
                    workingDirectory,
                    ref startup,
                    out processInfo))
            {
                return false;
            }

            pid = (int)processInfo.dwProcessId;
            return pid > 0;
        }
        finally
        {
            if (processInfo.hThread != IntPtr.Zero)
                NativeMethods.CloseHandle(processInfo.hThread);
            if (processInfo.hProcess != IntPtr.Zero)
                NativeMethods.CloseHandle(processInfo.hProcess);
            if (environment != IntPtr.Zero)
                NativeMethods.DestroyEnvironmentBlock(environment);
            if (primary != IntPtr.Zero)
                NativeMethods.CloseHandle(primary);
            NativeMethods.CloseHandle(userToken);
        }
    }
}
