using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using SshNet;

namespace DMT.SFTP.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly AppSettings _appSettings;
        private readonly string _userName;
        private readonly string _password;
        private readonly string _host;

        public Worker(ILogger<Worker> logger, IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _appSettings = appSettings.Value;
            _userName = _appSettings.Username;
            _password = _appSettings.Password;
            _host = _appSettings.Host;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Application is starting");
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Application is shutting down");
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {

                

                await Task.Delay(TimeSpan.FromMinutes(_appSettings.PollingIntervalMinutes), stoppingToken);
            }
        }

        private List<string> GetNewInboundFileNamesFromFTP()
        {
            var inboundFiles = new List<string>();

            using (var sftpClient = new Renci.SshNet.SftpClient(_host, _userName, _password))
            {
                sftpClient.Connect();
            }

                return inboundFiles;
        }

        private void ListSftpDirectory(SftpClient sftpClient, string directory, ref List<string> files)
        {
            foreach (var sftpEntry in sftpClient.ListDirectory(directory))
            {
                if(sftpEntry.IsRegularFile)
                {

                }
                else
                {
                    _logger.LogWarning($"Not a regular file: {sftpEntry.FullName}");
                }
            }
        }
    }
}
