using System.Diagnostics;
using System.Management;

namespace XCom2ModCompiler.Utilities;

/// <summary>
/// Utility methods for process management.
/// </summary>
public static class ProcessExtensions
{
    public static void KillProcessTree(int pid)
    {
        try
        {
            // Get all child processes
            var children = GetChildProcesses(pid);
            
            // Kill children first (recursive)
            foreach (var child in children)
            {
                KillProcessTree(child.Id);
            }
            
            // Kill parent
            using var process = Process.GetProcessById(pid);
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit(5000);
            }
        }
        catch (ArgumentException)
        {
            // Process already exited - ignore
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to kill process {pid}: {ex.Message}");
        }
    }

    private static List<Process> GetChildProcesses(int pid)
    {
        var children = new List<Process>();
        
        try
        {
            // Note: Requires System.Management package reference
            var searcher = new ManagementObjectSearcher(
                $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {pid}");
            
            foreach (var obj in searcher.Get())
            {
                var childPid = (uint)obj["ProcessId"];
                try
                {
                    children.Add(Process.GetProcessById((int)childPid));
                }
                catch (ArgumentException)
                {
                    // Child process already exited
                }
            }
        }
        catch
        {
            // WMI query failed - return empty list
        }
        
        return children;
    }
}
