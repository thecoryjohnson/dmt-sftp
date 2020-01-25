using System;
using System.Collections.Generic;
using System.Text;

namespace DMT.SFTP.Service
{
    public class AppSettings
    {
        public string Host { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int PollingIntervalMinutes { get; set; }
        public string TiContactInboundDirectory { get; set; }
        public string TiContactOutboundDirectory { get; set; }
        public string GkContactInboundDirectory { get; set; }
        public string GkContactOutboundDirectory { get; set; }
    }
}
