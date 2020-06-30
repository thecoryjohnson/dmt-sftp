using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace DMT.SFTP.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly AppSettings _appSettings;
        private readonly string _userName;
        private readonly string _password;
        private readonly string _host;
        private readonly string _tiContactOutboundDirectory;

        public Worker(ILogger<Worker> logger, IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _appSettings = appSettings.Value;
            _userName = _appSettings.Username;
            _password = _appSettings.Password;
            _host = _appSettings.Host;
            _tiContactOutboundDirectory = _appSettings.TiContactOutboundDirectory;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var sftpClient = new SftpClient(_host, 22, _userName, _password))
                {
                    try
                    {
                        sftpClient.Connect();

                        var inboundContactFiles = GetNewInboundFileNamesFromTi(sftpClient);
                        var outboundContactFiles = GetNewOutboundFileNamesFromGk(_appSettings.GkContactOutboundDirectory);

                        // Files inbound to Golden Key, from True Interation
                        if (inboundContactFiles.Count > 0)
                        {
                            _logger.LogInformation($"{inboundContactFiles.Count} files found in the true interation outbound site");

                            foreach (var sourceFile in inboundContactFiles)
                            {
                                try
                                {
                                    MoveFileFromTiSftpSiteToGk(sftpClient, sourceFile);
                                    DeleteRemoteSftpFile(sftpClient, sourceFile);
                                }
                                catch(Exception ex)
                                {
                                    _logger.LogError(ex, $"Error while moving {sourceFile.Name}");
                                }
                            }
                        }
                        else
                        {
                            _logger.LogInformation("No new contact files found in the true interaction outbound site");
                        }

                        // Files inbound to True Interation, from Golden Key
                        if (outboundContactFiles.Count > 0)
                        {
                            _logger.LogInformation($"{outboundContactFiles.Count} files found in the golden key outbound site");

                            foreach (var sourceFile in outboundContactFiles)
                            {
                                try
                                {
                                    MoveFileFromGkToTiSftpSite(sftpClient, sourceFile);
                                    DeleteLocalFile(sourceFile);
                                }
                                catch(Exception ex)
                                {
                                    _logger.LogError(ex, $"Error while moving {sourceFile}");
                                }
                            }
                        }
                        else
                        {
                            _logger.LogInformation("No new contact files found in the golden key outbound site");
                        }
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception");
                    }
                    finally
                    {
                        sftpClient.Disconnect();
                    }
                }
                await Task.Delay(TimeSpan.FromMinutes(_appSettings.PollingIntervalMinutes), stoppingToken);
            }
        }

        /// <summary>
        /// Returns a list of regular files (Wordpress extract) in the TI Contact outbound remote site
        /// </summary>
        /// <param name="sftpClient"></param>
        /// <returns></returns>
        private List<SftpFile> GetNewInboundFileNamesFromTi(SftpClient sftpClient)
        {
            var inboundFiles = new List<SftpFile>();
            
            ListSftpDirectory(sftpClient, _tiContactOutboundDirectory, ref inboundFiles);
            
            return inboundFiles;
        }

        /// <summary>
        /// Returns a list of files (Netforum extract) in the GK Contact outbound local site
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <returns></returns>
        private List<string> GetNewOutboundFileNamesFromGk(string sourceDirectory)
        {
            var outboundFiles = new List<string>();
            var directoryInfo = new DirectoryInfo(sourceDirectory);

            if(directoryInfo.Exists)
            {
                var files = directoryInfo.GetFiles();
                foreach(var file in files)
                {
                    outboundFiles.Add(file.Name);
                }
            }
            else
            {
                _logger.LogError($"Cannot locate directory {sourceDirectory}");
            }

            return outboundFiles;
        }

        /// <summary>
        /// Returns a list of files in a SFTP remote site
        /// </summary>
        /// <param name="sftpClient"></param>
        /// <param name="directory"></param>
        /// <param name="files"></param>
        private void ListSftpDirectory(SftpClient sftpClient, string directory, ref List<SftpFile> files)
        {
            foreach (var sftpEntry in sftpClient.ListDirectory(directory))
            {
                if(sftpEntry.IsRegularFile)
                {
                    files.Add(sftpEntry);
                }
            }
        }

        /// <summary>
        /// Downloads the Outbound Contact files from Wordpress
        /// </summary>
        /// <param name="sftpClient"></param>
        /// <param name="sourceFile"></param>
        private void MoveFileFromTiSftpSiteToGk(SftpClient sftpClient, SftpFile sourceFile)
        {
            var localFilePath = _appSettings.GkContactInboundDirectory + sourceFile.Name;

            using (Stream localFile = File.Create(localFilePath))
            {
                _logger.LogInformation($"Moving {sourceFile.Name} to {localFilePath}");

                sftpClient.DownloadFile(sourceFile.FullName, localFile);

                _logger.LogInformation($"Move complete: {localFilePath}");
            }
        }

        /// <summary>
        /// Uploads the Outbound Contact files from Netforum
        /// </summary>
        /// <param name="sftpClient"></param>
        /// <param name="sourceFile"></param>
        private void MoveFileFromGkToTiSftpSite(SftpClient sftpClient, string sourceFile)
        {
            var localFilePath = _appSettings.GkContactOutboundDirectory + sourceFile;
            var remotePath = _appSettings.TiContactInboundDirectory + sourceFile;

            using (Stream localFile = File.OpenRead(localFilePath))
            {
                _logger.LogInformation($"Moving Netforum extract file {sourceFile}");

                sftpClient.UploadFile(localFile, remotePath);

                _logger.LogInformation($"{sourceFile} moved from Gk to remote site: {remotePath}");
            }
        }

        /// <summary>
        /// Removes the remote SFTP file
        /// </summary>
        /// <param name="sftpClient"></param>
        /// <param name="sftpFile"></param>
        private void DeleteRemoteSftpFile(SftpClient sftpClient, SftpFile sftpFile)
        {
            _logger.LogInformation($"Deleting file {sftpFile.Name} from remote site");
            
            sftpClient.DeleteFile(sftpFile.FullName);

            _logger.LogInformation($"{sftpFile.Name} deleted");
        }

        /// <summary>
        /// Deletes a file on the local host file system
        /// </summary>
        /// <param name="filePath"></param>
        private void DeleteLocalFile(string filePath)
        {
            var localFilePath = _appSettings.GkContactOutboundDirectory + filePath;

            _logger.LogInformation($"Deleting {localFilePath}");

            File.Delete(localFilePath);

            _logger.LogInformation($"{localFilePath} deleted");
        }
    }
}
