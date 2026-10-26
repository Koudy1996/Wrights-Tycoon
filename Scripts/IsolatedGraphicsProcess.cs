using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace WrightTycoon.Workflow
{
    // Isolate window ownership, not just the initial show state. Unity utility windows
    // can override SW_HIDE; they cannot escape their process's assigned desktop.
    public static class IsolatedGraphicsProcess
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int Size;
            public string Reserved, Desktop, Title;
            public int X, Y, Width, Height, XChars, YChars, Fill, Flags;
            public short Show, ReservedSize;
            public IntPtr ReservedData, Input, Output, Error;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInfo { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
        [StructLayout(LayoutKind.Sequential)]
        private struct BasicJobLimits
        {
            public long ProcessTime, JobTime;
            public uint Flags;
            public UIntPtr MinWorkingSet, MaxWorkingSet;
            public uint ActiveProcesses;
            public UIntPtr Affinity;
            public uint Priority, Scheduling;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)]
        private struct JobLimits
        {
            public BasicJobLimits Basic;
            public IoCounters Io;
            public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, uint flags, uint access, IntPtr security);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetThreadDesktop(uint thread);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, int length, out int required);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processSecurity,
            IntPtr threadSecurity, bool inherit, uint flags, IntPtr environment, string directory,
            ref StartupInfo startup, out ProcessInfo process);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr security, string name);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref JobLimits limits, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr process, uint code);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);

        private static Win32Exception Error(string operation) { return new Win32Exception(Marshal.GetLastWin32Error(), operation); }
        private static string DesktopName(IntPtr desktop)
        {
            if (desktop == IntPtr.Zero) throw Error("Open desktop for inspection");
            int length;
            var name = new StringBuilder(512);
            if (!GetUserObjectInformation(desktop, 2, name, name.Capacity * 2, out length)) throw Error("Read desktop name");
            return name.ToString();
        }
        public static string CurrentDesktopName() { return DesktopName(GetThreadDesktop(GetCurrentThreadId())); }
        public static string InputDesktopName()
        {
            var desktop = OpenInputDesktop(0, false, 1);
            try { return DesktopName(desktop); }
            finally { if (desktop != IntPtr.Zero) CloseDesktop(desktop); }
        }

        public static int Run(string executable, string arguments, string directory, string desktopName, int timeoutSeconds)
        {
            if (String.IsNullOrWhiteSpace(desktopName) || !desktopName.StartsWith("WrightTycoonTests-", StringComparison.Ordinal)
                || desktopName.IndexOf('\\') >= 0 || desktopName.IndexOf('/') >= 0)
                throw new ArgumentException("A unique isolated test desktop name is required.", "desktopName");
            if (timeoutSeconds < 1) throw new ArgumentOutOfRangeException("timeoutSeconds");
            var desktop = IntPtr.Zero;
            var job = IntPtr.Zero;
            var process = new ProcessInfo();
            try
            {
                // Never call SwitchDesktop or SetThreadDesktop on the workflow process.
                desktop = CreateDesktop(desktopName, IntPtr.Zero, IntPtr.Zero, 0, 0x01FF, IntPtr.Zero);
                if (desktop == IntPtr.Zero) throw Error("Create isolated graphics desktop; no visible fallback is permitted");
                job = CreateJobObject(IntPtr.Zero, null);
                if (job == IntPtr.Zero) throw Error("Create isolated graphics process job");
                var limits = new JobLimits();
                limits.Basic.Flags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf(typeof(JobLimits))))
                    throw Error("Configure graphics process cleanup");
                var startup = new StartupInfo { Size = Marshal.SizeOf(typeof(StartupInfo)), Desktop = desktopName };
                var command = new StringBuilder("\"" + executable + "\" " + arguments);
                // Suspend until the job owns the process, so child processes cannot outlive cleanup.
                if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, false, 4, IntPtr.Zero, directory, ref startup, out process))
                    throw Error("Launch Unity on isolated desktop");
                if (!AssignProcessToJobObject(job, process.Process)) throw Error("Assign isolated graphics process job");
                if (ResumeThread(process.Thread) == UInt32.MaxValue) throw Error("Start isolated graphics process");
                var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
                while (true)
                {
                    var wait = WaitForSingleObject(process.Process, 1000);
                    if (wait == 0) break;
                    if (wait != 258) throw Error("Wait for isolated graphics process");
                    if (DateTime.UtcNow >= deadline) throw new TimeoutException("Isolated graphics test process exceeded its time limit.");
                }
                uint exitCode;
                if (!GetExitCodeProcess(process.Process, out exitCode)) throw Error("Read graphics process exit code");
                return unchecked((int)exitCode);
            }
            finally
            {
                // Also terminate a suspended process if job assignment failed.
                if (process.Process != IntPtr.Zero && WaitForSingleObject(process.Process, 0) == 258)
                {
                    TerminateProcess(process.Process, 1);
                    WaitForSingleObject(process.Process, 5000);
                }
                if (job != IntPtr.Zero) CloseHandle(job);
                if (process.Thread != IntPtr.Zero) CloseHandle(process.Thread);
                if (process.Process != IntPtr.Zero) CloseHandle(process.Process);
                if (desktop != IntPtr.Zero) CloseDesktop(desktop);
            }
        }
    }
}
