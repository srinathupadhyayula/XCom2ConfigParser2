using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace X2BuildTasks
{
    /// <summary>
    /// Utility class to invoke PowerShell scripts as part of the build process
    /// </summary>
    public class InvokePowershellTask
    {
        public static int ExecutePowershellScript(string scriptPath, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" {string.Join(" ", arguments)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(scriptPath)
            };

            using (var process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                    Console.WriteLine(output);
                
                if (!string.IsNullOrEmpty(error))
                    Console.WriteLine(error);

                return process.ExitCode;
            }
        }
    }
}