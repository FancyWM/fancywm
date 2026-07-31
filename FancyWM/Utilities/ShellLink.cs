using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FancyWM.Utilities
{
    internal static class ShellLink
    {
        public static void Create(string linkPath, string targetPath, string? description = null)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell is not registered.");

            dynamic? shell = null;
            dynamic? link = null;
            try
            {
                shell = Activator.CreateInstance(shellType)
                    ?? throw new InvalidOperationException("Failed to create WScript.Shell.");
                link = shell.CreateShortcut(linkPath);
                link.TargetPath = targetPath;
                link.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
                if (description != null)
                {
                    link.Description = description;
                }
                link.Save();
            }
            finally
            {
                if (link != null)
                {
                    Marshal.FinalReleaseComObject(link);
                }
                if (shell != null)
                {
                    Marshal.FinalReleaseComObject(shell);
                }
            }
        }
    }
}
