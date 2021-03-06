﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace TwitchLeecher.Setup.Publish
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    throw new ArgumentException("Missing arguments in main method", "args");
                }

                string solutionDir = args[0];

                if (!Directory.Exists(solutionDir))
                {
                    throw new ApplicationException("SolutionDir '" + solutionDir + "' does not exist!");
                }

                Version version = Assembly.LoadFile(Path.Combine(solutionDir, "..", "TwitchLeecher", "TwitchLeecher", "bin", "TwitchLeecher.exe")).GetName().Version.Trim();

                string srcX86 = Path.Combine(solutionDir, "TwitchLeecher.Setup.Bootstrapper.x86", "bin", "TwitchLeecher_x86.exe");
                string srcX64 = Path.Combine(solutionDir, "TwitchLeecher.Setup.Bootstrapper.x64", "bin", "TwitchLeecher_x64.exe");

                string tgtFilenameX86 = "TwitchLeecher_" + version.ToString() + "_x86.exe";
                string tgtFilenameX64 = "TwitchLeecher_" + version.ToString() + "_x64.exe";

                string resHacker = Path.Combine(solutionDir, "Libraries", "ResourceHacker.exe");
                string publishDir = Path.Combine(solutionDir, "..", "TwitchLeecher.Setup.Publish");

                string tgt32bit = Path.Combine(publishDir, tgtFilenameX86);
                string tgt64bit = Path.Combine(publishDir, tgtFilenameX64);

                string manifest32bit = Path.Combine(publishDir, tgtFilenameX86 + ".manifest");
                string manifest64bit = Path.Combine(publishDir, tgtFilenameX64 + ".manifest");

                CleanDirectory(publishDir);

                CopyFile(srcX86, publishDir, tgtFilenameX86);
                CopyFile(srcX64, publishDir, tgtFilenameX64);

                ExtractManifest(resHacker, tgt32bit, manifest32bit);
                ExtractManifest(resHacker, tgt64bit, manifest64bit);

                ModifyManifest(manifest32bit);
                ModifyManifest(manifest64bit);

                UpdateManifest(resHacker, tgt32bit, manifest32bit);
                UpdateManifest(resHacker, tgt64bit, manifest64bit);

                DeleteFile(manifest32bit);
                DeleteFile(manifest64bit);
            }
            catch
            {
                return -1;
            }

            return 0;
        }

        private static void ExtractManifest(string resHacker, string sourceFile, string manifestFile)
        {
            ProcessStartInfo psi = new ProcessStartInfo(resHacker);
            psi.Arguments = "-extract \"" + sourceFile + "\",\"" + manifestFile + "\",24,1,1033";
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;

            using (Process process = new Process() { StartInfo = psi })
            {
                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new ApplicationException("Extracting manifest from file '" + sourceFile + "' failed!");
                }
            }
        }

        private static void ModifyManifest(string manifestFile)
        {
            XDocument xDoc = XDocument.Load(manifestFile);

            XElement execLevelEl = xDoc.Descendants(XName.Get("requestedExecutionLevel", "urn:schemas-microsoft-com:asm.v3")).FirstOrDefault();

            if (execLevelEl == null)
            {
                throw new ApplicationException("XElement 'requestedExecutionLevel' not found!");
            }

            XAttribute levelAttr = execLevelEl.Attribute("level");

            if (levelAttr == null)
            {
                throw new ApplicationException("XAttribute 'level' not found!");
            }

            levelAttr.Value = "requireAdministrator";

            xDoc.Save(manifestFile);
        }

        private static void UpdateManifest(string resHacker, string targetFile, string manifestFile)
        {
            ProcessStartInfo psi = new ProcessStartInfo(resHacker);
            psi.Arguments = "-modify \"" + targetFile + "\",\"" + targetFile + "\",\"" + manifestFile + "\",24,1,1033";
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;

            using (Process process = new Process() { StartInfo = psi })
            {
                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new ApplicationException("Modifying manifest in file '" + targetFile + "' failed!");
                }
            }
        }

        private static void ResetFileAttributes(string file)
        {
            if (File.Exists(file))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
        }

        private static void DeleteFile(string file)
        {
            FileInfo fileInfo = new FileInfo(file);

            if (fileInfo.Exists)
            {
                ResetFileAttributes(fileInfo.FullName);
                fileInfo.Delete();
            }
        }

        private static void CreateDirectory(string directory)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(directory);

            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }
        }

        private static void DeleteDirectory(string directory)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(directory);

            if (dirInfo.Exists)
            {
                CleanDirectory(directory);
                dirInfo.Delete(true);
            }
        }

        private static void CleanDirectory(string directory)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(directory);

            if (dirInfo.Exists)
            {
                foreach (FileInfo file in dirInfo.GetFiles())
                {
                    DeleteFile(file.FullName);
                }

                foreach (DirectoryInfo dir in dirInfo.GetDirectories())
                {
                    DeleteDirectory(dir.FullName);
                }
            }
        }

        private static void CopyFile(string sourceFile, string targetDir, string newFileName = null)
        {
            CreateDirectory(targetDir);

            FileInfo fileInfo = new FileInfo(sourceFile);

            string targetFile = Path.Combine(targetDir, newFileName ?? fileInfo.Name);

            ResetFileAttributes(targetFile);

            File.Copy(fileInfo.FullName, targetFile, true);
        }
    }

    public static class Extensions
    {
        public static Version Trim(this Version version)
        {
            if (version.Build > 0 && version.Revision > 0)
            {
                return version;
            }
            else if (version.Build > 0)
            {
                return Version.Parse(version.ToString(3));
            }
            else
            {
                return Version.Parse(version.ToString(2));
            }
        }
    }
}