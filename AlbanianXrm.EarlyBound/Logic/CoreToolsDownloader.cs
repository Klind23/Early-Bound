﻿using System.IO;
using System.Linq;
using System.Windows.Forms;
using XrmToolBox.Extensibility;

namespace AlbanianXrm.EarlyBound.Logic
{
    internal class CoreToolsDownloader
    {
        MyPluginControl myPlugin;

        public CoreToolsDownloader(MyPluginControl myPlugin)
        {
            this.myPlugin = myPlugin;
        }

        public void DownloadCoreTools()
        {
            myPlugin.WorkAsync(new WorkAsyncInfo
            {
                Message = $"Getting latest version of Core Tools",
                Work = (worker, args) =>
                {
                    //ID of the package to be looked 
                    string packageID = "Microsoft.CrmSdk.CoreTools";

                    //Connect to the official package repository IPackageRepository
                    var repo = NuGet.PackageRepositoryFactory.Default.CreateRepository("https://packages.nuget.org/api/v2");

                    string dir = Path.GetDirectoryName(typeof(MyPluginControl).Assembly.Location).ToLower();
                    string folder = Path.GetFileNameWithoutExtension(typeof(MyPluginControl).Assembly.Location);
                    dir = Path.Combine(dir, folder);
                    Directory.CreateDirectory(dir);
                    NuGet.PackageManager packageManager = new NuGet.PackageManager(repo, dir);
                    var package = repo.GetPackages().Where(x => x.Id == packageID && x.IsLatestVersion).FirstOrDefault();
                    foreach (var file in package.GetFiles())
                    {
                        using (var stream = File.Create(Path.Combine(dir, Path.GetFileName(file.Path))))
                            file.GetStream().CopyTo(stream);
                    }
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            });

        }
    }
}
