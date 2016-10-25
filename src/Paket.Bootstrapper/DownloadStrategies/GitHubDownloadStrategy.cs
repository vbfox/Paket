﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.DownloadStrategies
{
    public class GitHubDownloadStrategy : IDownloadStrategy
    {
        public static class Constants
        {
            public const string PaketReleasesLatestUrl = "https://github.com/fsprojects/Paket/releases/latest";
            public const string PaketReleasesUrl = "https://github.com/fsprojects/Paket/releases";
            public const string PaketExeDownloadUrlTemplate = "https://github.com/fsprojects/Paket/releases/download/{0}/paket.exe";
        }

        private IWebRequestProxy WebRequestProxy { get; set; }
        private IFileSystemProxy FileSystemProxy { get; set; }
        public string Name { get { return "Github"; } }
        public IDownloadStrategy FallbackStrategy { get; set; }

        public GitHubDownloadStrategy(IWebRequestProxy webRequestProxy, IFileSystemProxy fileSystemProxy)
        {
            WebRequestProxy = webRequestProxy;
            FileSystemProxy = fileSystemProxy;
        }

        public string GetLatestVersion(bool ignorePrerelease)
        {
            var latestStable = GetLatestStable();
            if (ignorePrerelease)
                return latestStable;
            else
                return Max(GetLatestPrerelease(), latestStable);
        }

        private string Max(string prerelease, string latestStable)
        {
            var greater = new[] { prerelease, latestStable }.Where(x => !string.IsNullOrEmpty(x)).Select(SemVer.Create).OrderByDescending(x => x).FirstOrDefault();
            if (greater == null) return "";
            return greater.Original;
        }

        private string GetLatestPrerelease()
        {
            var data = WebRequestProxy.DownloadString(Constants.PaketReleasesUrl);
            return GetVersions(data).FirstOrDefault(s => s.Contains("-"));
        }

        private string GetLatestStable()
        {
            var data = WebRequestProxy.DownloadString(Constants.PaketReleasesLatestUrl);
            var title = data.Substring(data.IndexOf("<title>") + 7, (data.IndexOf("</title>") + 8) - (data.IndexOf("<title>") + 7)); // grabs everything in the <title> tag
            var version = title.Split(' ')[1]; // Release, 1.34.0, etc, etc, etc <-- the release number is the second part fo this split string
            return version;
        }

        private List<string> GetVersions(string data)
        {
            var start = 0;
            var versions = new List<string>();
            while ((start = data.IndexOf("Paket/tree/", start)) != -1)
            {
                start = start + 11;
                var end = data.IndexOf("\"", start);
                var latestVersion = data.Substring(start, end - start);
                if (!versions.Contains(latestVersion)) versions.Add(latestVersion);
            }
            return versions;
        }

        public void DownloadVersion(string latestVersion, string target)
        {
            var url = String.Format(Constants.PaketExeDownloadUrlTemplate, latestVersion);
            ConsoleImpl.WriteDebug("Starting download from {0}", url);

            var tmpFile = BootstrapperHelper.GetTempFile("paket");
            WebRequestProxy.DownloadFile(url, tmpFile);

            FileSystemProxy.CopyFile(tmpFile, target, true);
            FileSystemProxy.DeleteFile(tmpFile);
        }

        public void SelfUpdate(string latestVersion)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            string exePath = executingAssembly.Location;
            var localVersion = FileSystemProxy.GetLocalFileVersion(exePath);
            if (localVersion.StartsWith(latestVersion))
            {
                ConsoleImpl.WriteDebug("Bootstrapper is up to date. Nothing to do.");
                return;
            }

            var url = String.Format("https://github.com/fsprojects/Paket/releases/download/{0}/paket.bootstrapper.exe", latestVersion);
            ConsoleImpl.WriteDebug("Starting download of bootstrapper from {0}", url);

            string renamedPath = BootstrapperHelper.GetTempFile("oldBootstrapper");
            string tmpDownloadPath = BootstrapperHelper.GetTempFile("newBootstrapper");
            WebRequestProxy.DownloadFile(url, tmpDownloadPath);

            try
            {
                FileSystemProxy.MoveFile(exePath, renamedPath);
                FileSystemProxy.MoveFile(tmpDownloadPath, exePath);
                ConsoleImpl.WriteDebug("Self update of bootstrapper was successful.");
            }
            catch (Exception)
            {
                ConsoleImpl.WriteDebug("Self update failed. Resetting bootstrapper.");
                FileSystemProxy.MoveFile(renamedPath, exePath);
                throw;
            }
        }

    }
}