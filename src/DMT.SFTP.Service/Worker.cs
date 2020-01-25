using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    sftpClient.Connect();

                    var inboundContactFiles = GetNewInboundFileNamesFromTi(sftpClient);
                    var outboundContactFiles = GetNewOutboundFileNamesFromGk(_appSettings.GkContactOutboundDirectory);

                    try
                    {
                        
                        if (inboundContactFiles.Count > 0)
                        {
                            _logger.LogInformation($"{inboundContactFiles.Count} files found in the true interation outbound site");

                            foreach (var sourceFile in inboundContactFiles)
                            {
                                MoveFileFromTiSftpSiteToGk(sftpClient, sourceFile);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("No new contact files found in the true interaction outbound site");
                        }

                        if (outboundContactFiles.Count > 0)
                        {
                            _logger.LogInformation($"{outboundContactFiles.Count} files found in the golden key outbound site");
                            foreach (var sourceFile in outboundContactFiles)
                            {
                                _logger.LogInformation($"Moving file {sourceFile}");
                            }
                        }
                        else
                        {
                            _logger.LogInformation("No new contact files found in the golden key outbound site");
                        }
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError($"Unhandled exception: {ex.Message}");
                    }
                    finally
                    {
                        sftpClient.Disconnect();
                    }
                }
                
                await Task.Delay(2000, stoppingToken);
            }
        }

        private List<SftpFile> GetNewInboundFileNamesFromTi(SftpClient sftpClient)
        {
            var inboundFiles = new List<SftpFile>();
            
            ListSftpDirectory(sftpClient, _tiContactOutboundDirectory, ref inboundFiles);
            
            return inboundFiles;
        }

        private List<string> GetNewOutboundFileNamesFromGk(string sourceDirectory)
        {
            var outboundFiles = new List<string>();
            var directoryInfo = new DirectoryInfo(sourceDirectory);

            if(directoryInfo.Exists)
            {
                var files = directoryInfo.GetFiles();
                foreach(var file in files)
                {
                    outboundFiles.Add(file.FullName);
                }
            }
            else
            {
                _logger.LogError($"Cannot locate directory {sourceDirectory}");
            }

            return outboundFiles;
        }

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

        private void MoveFileFromTiSftpSiteToGk(SftpClient sftpClient, SftpFile sourceFile)
        {
            var localFilePath = _appSettings.GkContactInboundDirectory + sourceFile.Name;

            using (Stream localFile = File.Create(localFilePath))
            {
                sftpClient.DownloadFile(sourceFile.FullName, localFile);
            }
        }
    }
}
