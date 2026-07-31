using System;
using System.IO;

using FancyWM.Utilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Tests.Utilities
{
    [TestClass]
    public class ShellLinkTest
    {
        [TestMethod]
        public void TestCreateResolvesToTarget()
        {
            var dir = Path.Combine(Path.GetTempPath(), "FancyWM.ShellLinkTest." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var target = Path.Combine(dir, "Target.exe");
                File.WriteAllBytes(target, new byte[] { 0x4D, 0x5A });
                var linkPath = Path.Combine(dir, "Link.lnk");

                ShellLink.Create(linkPath, target, "FancyWM");

                Assert.IsTrue(File.Exists(linkPath));
                Assert.AreEqual(target, ReadTargetPath(linkPath));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [TestMethod]
        public void TestCreateOverwritesExistingLink()
        {
            var dir = Path.Combine(Path.GetTempPath(), "FancyWM.ShellLinkTest." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var first = Path.Combine(dir, "First.exe");
                var second = Path.Combine(dir, "Second.exe");
                File.WriteAllBytes(first, new byte[] { 0x4D, 0x5A });
                File.WriteAllBytes(second, new byte[] { 0x4D, 0x5A });
                var linkPath = Path.Combine(dir, "Link.lnk");

                ShellLink.Create(linkPath, first);
                ShellLink.Create(linkPath, second);

                Assert.AreEqual(second, ReadTargetPath(linkPath));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        private static string ReadTargetPath(string linkPath)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(linkPath);
            return (string)link.TargetPath;
        }
    }
}
