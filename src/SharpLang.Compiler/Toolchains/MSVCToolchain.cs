using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SharpLang.Toolsets
{
    public class MSVCToolchain
    {
        public static List<string> GetSystemIncludes()
        {
            var includes = new List<string>();

            string vsSdkDir;
            if (GetVisualStudioDir(out vsSdkDir))
                includes.Add(vsSdkDir + "\\VC\\include");

            string windowsSdkDir;
            if (GetWindowsSDKDir(out windowsSdkDir))
                includes.Add(windowsSdkDir + "\\include");
            else if (!string.IsNullOrEmpty(vsSdkDir))
                includes.Add(vsSdkDir + "\\VC\\PlatformSDK\\Include");

            return includes;
        }

        #region Helpers

        public struct ToolchainVersion
        {
            public float Version;
            public string Directory;
        }

        /// Get .NET framework installation directory.
        public static bool GetNetFrameworkDir(out string path)
        {
            var versions = new List<ToolchainVersion>();

            // Try the Windows registry.
            var hasSDKDir = GetSystemRegistryString(
                "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\.NETFramework",
                "InstallRoot", versions, RegistryView.Registry32);

            if (versions.Count == 0 && Environment.Is64BitProcess)
            {
                hasSDKDir = GetSystemRegistryString(
                    "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\.NETFramework",
                    "InstallRoot", versions, RegistryView.Registry64);
            }

            path = null;
            if (versions.Count == 0)
                return false;

            // Pick the highest found SDK version.
            versions.Sort((v1, v2) => (int)(v1.Version - v2.Version));

            path = versions.Last().Directory;
            return true;
        }

        /// Get MSBuild installation directory.
        public static bool GetMSBuildSDKDir(out string path)
        {
            var versions = new List<ToolchainVersion>();

            // Try the Windows registry.
            var hasSDKDir = GetSystemRegistryString(
                "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\MSBuild\\ToolsVersions\\$VERSION",
                "MSBuildToolsPath", versions, RegistryView.Registry32);

            if (versions.Count == 0 && Environment.Is64BitProcess)
            {
                hasSDKDir = GetSystemRegistryString(
                    "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\MSBuild\\ToolsVersions\\$VERSION",
                    "MSBuildToolsPath", versions, RegistryView.Registry64);
            }

            path = null;
            if (versions.Count == 0)
                return false;

            // Pick the highest found SDK version.
            versions.Sort((v1, v2) => (int)(v1.Version - v2.Version));

            path = versions.Last().Directory;
            return true;
        }

        /// Get Windows SDK installation directory.
        public static bool GetWindowsSDKDir(out string path)
        {
            var versions = new List<ToolchainVersion>();

            // Try the Windows registry.
            var hasSDKDir = GetSystemRegistryString(
                "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Microsoft SDKs\\Windows\\$VERSION",
                "InstallationFolder", versions, RegistryView.Registry32);

            if (versions.Count == 0 && Environment.Is64BitProcess)
            {
                hasSDKDir = GetSystemRegistryString(
                    "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Microsoft SDKs\\Windows\\$VERSION",
                    "InstallationFolder", versions, RegistryView.Registry64);
            }

            path = null;
            if (versions.Count == 0)
                return false;

            // Pick the highest found SDK version.
            versions.Sort((v1, v2) => (int)(v1.Version - v2.Version));

            path = versions.Last().Directory;
            return true;
        }

        /// Get Visual Studio installation directory.
        public static List<ToolchainVersion> GetVisualStudioDir()
        {
            // Then try the windows registry.
            var versions = new List<ToolchainVersion>();
            var hasVCDir = GetSystemRegistryString(
                "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\VisualStudio\\$VERSION",
                "InstallDir", versions, RegistryView.Registry32);

            if (versions.Count == 0 && Environment.Is64BitProcess)
            {
                hasVCDir = GetSystemRegistryString(
                    "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\VisualStudio\\$VERSION",
                    "InstallDir", versions, RegistryView.Registry64);
            }

            var hasVCExpressDir = GetSystemRegistryString(
                "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\VCExpress\\$VERSION",
                "InstallDir", versions, RegistryView.Registry32);

            if (versions.Count == 0 && Environment.Is64BitProcess)
            {
                hasVCExpressDir = GetSystemRegistryString(
                    "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\VCExpress\\$VERSION",
                    "InstallDir", versions, RegistryView.Registry64);
            }

            for (var tc = 0; tc < versions.Count; tc++)
            {
                var toolchain = versions[tc];
                var dir = toolchain.Directory;
                toolchain.Directory = dir.Substring(0, dir.LastIndexOf("\\Common7\\IDE",
                    StringComparison.Ordinal));
                versions[tc] = toolchain;
            }

            return versions;
        }

        /// Get Visual Studio installation directory.
        public static bool GetVisualStudioDir(out string path)
        {
            var versions = GetVisualStudioDir();

            path = null;
            if (versions.Count == 0)
                return false;

            // Pick the highest found SDK version.
            versions.Sort((v1, v2) => (int)(v1.Version - v2.Version));

            path = versions.Last().Directory;
            return true;
        }

        /// Read registry string.
        /// This also supports a means to look for high-versioned keys by use
        /// of a $VERSION placeholder in the key path.
        /// $VERSION in the key path is a placeholder for the version number,
        /// causing the highest value path to be searched for and used.
        /// I.e. "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\VisualStudio\\$VERSION".
        /// There can be additional characters in the component.  Only the numeric
        /// characters are compared.
        static bool GetSystemRegistryString(string keyPath, string valueName,
            ICollection<ToolchainVersion> entries, RegistryView view)
        {
            string subKey;
            var hive = GetRegistryHive(keyPath, out subKey);
            var rootKey = RegistryKey.OpenBaseKey(hive, view);

            var versionPosition = subKey.IndexOf("\\$VERSION", StringComparison.Ordinal);
            if (versionPosition <= 0)
                return false;

            // If we have a $VERSION placeholder, do the highest-version search.
            var partialKey = subKey.Substring(0, versionPosition);
            using (var key = rootKey.OpenSubKey(partialKey, writable: false))
            {
                if (key == null)
                    return false;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    // Get the number version from the key.
                    var match = Regex.Match(subKeyName, @"[1-9][0-9]*\.?[0-9]*");
                    if (!match.Success)
                        continue;

                    var versionText = match.Groups[0].Value;

                    float version;
                    float.TryParse(versionText, NumberStyles.Number,
                        CultureInfo.InvariantCulture, out version);

                    using (var versionKey = key.OpenSubKey(subKeyName))
                    {
                        // Check that the key has a value passed by the caller.
                        var keyValue = versionKey.GetValue(valueName);
                        if (keyValue == null)
                            continue; // Skip this version since it's invalid.

                        var entry = new ToolchainVersion
                        {
                            Version = version,
                            Directory = keyValue.ToString()
                        };

                        entries.Add(entry);
                    }
                }
            }

            return true;
        }

        static RegistryHive GetRegistryHive(string keyPath, out string subKey)
        {
            var hive = (RegistryHive)0;
            subKey = null;

            if (keyPath.StartsWith("HKEY_CLASSES_ROOT\\"))
            {
                hive = RegistryHive.ClassesRoot;
                subKey = keyPath.Substring(18);
            }
            else if (keyPath.StartsWith("HKEY_USERS\\"))
            {
                hive = RegistryHive.Users;
                subKey = keyPath.Substring(11);
            }
            else if (keyPath.StartsWith("HKEY_LOCAL_MACHINE\\"))
            {
                hive = RegistryHive.LocalMachine;
                subKey = keyPath.Substring(19);
            }
            else if (keyPath.StartsWith("HKEY_CURRENT_USER\\"))
            {
                hive = RegistryHive.CurrentUser;
                subKey = keyPath.Substring(18);
            }

            return hive;
        }

        #endregion
    }
}