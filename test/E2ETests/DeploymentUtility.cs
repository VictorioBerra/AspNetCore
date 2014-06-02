﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace E2ETests
{
    internal class DeploymentUtility
    {
        private static string GetIISExpressPath()
        {
            var iisExpressPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "IIS Express", "iisexpress.exe");

            //If X86 version does not exist
            if (!File.Exists(iisExpressPath))
            {
                iisExpressPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS Express", "iisexpress.exe");

                if (!File.Exists(iisExpressPath))
                {
                    throw new Exception("Unable to find IISExpress on the machine");
                }
            }

            return iisExpressPath;
        }

        /// <summary>
        /// Copy AspNet.Loader.dll to bin folder
        /// </summary>
        /// <param name="applicationPath"></param>
        private static void CopyAspNetLoader(string applicationPath)
        {
            string packagesDirectory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, @"..\..\Packages"));
            var aspNetLoaderSrcPath = Path.Combine(Directory.GetDirectories(packagesDirectory, "Microsoft.AspNet.Loader.IIS.Interop.*").First(), @"tools\AspNet.Loader.dll");
            var aspNetLoaderDestPath = Path.Combine(applicationPath, @"bin\AspNet.Loader.dll");
            if (!File.Exists(aspNetLoaderDestPath))
            {
                File.Copy(aspNetLoaderSrcPath, aspNetLoaderDestPath);
            }
        }

        private const string APP_RELATIVE_PATH = @"..\..\src\MusicStore\";

        public static Process StartApplication(HostType hostType, KreFlavor kreFlavor, string identityDbName)
        {
            string applicationPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, APP_RELATIVE_PATH));

            if (hostType == HostType.Helios)
            {
                return StartHeliosHost(applicationPath);
            }
            else
            {
                return StartSelfHost(applicationPath, identityDbName);
            }
        }

        private static Process StartHeliosHost(string applicationPath)
        {
            CopyAspNetLoader(applicationPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = GetIISExpressPath(),
                Arguments = string.Format("/port:5001 /path:{0}", applicationPath),
                UseShellExecute = true,
                CreateNoWindow = true
            };

            var hostProcess = Process.Start(startInfo);
            Console.WriteLine("Started iisexpress. Process Id : {0}", hostProcess.Id);

            return hostProcess;
        }

        private static Process StartSelfHost(string applicationPath, string identityDbName)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "klr.exe",
                Arguments = string.Format("--appbase {0} \"Microsoft.Framework.ApplicationHost\" web", applicationPath),
                UseShellExecute = true,
                CreateNoWindow = true
            };

            var hostProcess = Process.Start(startInfo);
            Console.WriteLine("Started klr.exe. Process Id : {0}", hostProcess.Id);
            WaitTillDbCreated(identityDbName);

            return hostProcess;
        }

        //In case of self-host application activation happens immediately unlike iis where activation happens on first request.
        //So in self-host case, we need a way to block the first request until the application is initialized. In MusicStore application's case, 
        //identity DB creation is pretty much the last step of application setup. So waiting on this event will help us wait efficiently.
        private static void WaitTillDbCreated(string identityDbName)
        {
            var identityDBFullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), identityDbName + ".mdf");
            if (File.Exists(identityDBFullPath))
            {
                Console.WriteLine("Database file '{0}' exists. Proceeding with the tests.", identityDBFullPath);
                return;
            }

            Console.WriteLine("Watching for the DB file '{0}'", identityDBFullPath);
            var dbWatch = new FileSystemWatcher(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), identityDbName + ".mdf");
            dbWatch.EnableRaisingEvents = true;

            try
            {
                if (!File.Exists(identityDBFullPath))
                {
                    //Wait for a maximum of 1 minute assuming the slowest cold start.
                    var watchResult = dbWatch.WaitForChanged(WatcherChangeTypes.Created, 60 * 1000);
                    if (watchResult.ChangeType == WatcherChangeTypes.Created)
                    {
                        Console.WriteLine("Database file created '{0}'. Proceeding with the tests.", identityDBFullPath);
                    }
                    else
                    {
                        Console.WriteLine("Database file '{0}' not created", identityDBFullPath);
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Received this exception while watching for Database file {0}", exception);
            }
            finally
            {
                dbWatch.Dispose();
            }
        }
    }
}