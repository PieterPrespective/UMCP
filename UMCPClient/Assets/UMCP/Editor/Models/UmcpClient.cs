namespace UMCP.Editor.Models
{
    public class UmcpClient
    {
        public string name;
        public string windowsConfigPath;
        public string linuxConfigPath;
        public UmcpTypes umcpType;
        public string configStatus;
        public UmcpStatus status = UmcpStatus.NotConfigured;

        // Helper method to convert the enum to a display string
        public string GetStatusDisplayString()
        {
            return status switch
            {
                UmcpStatus.NotConfigured => "Not Configured",
                UmcpStatus.Configured => "Configured",
                UmcpStatus.Running => "Running",
                UmcpStatus.Connected => "Connected",
                UmcpStatus.IncorrectPath => "Incorrect Path",
                UmcpStatus.CommunicationError => "Communication Error",
                UmcpStatus.NoResponse => "No Response",
                UmcpStatus.UnsupportedOS => "Unsupported OS",
                UmcpStatus.MissingConfig => "Missing UMCP Config",
                UmcpStatus.Error => configStatus.StartsWith("Error:") ? configStatus : "Error",
                _ => "Unknown"
            };
        }

        // Helper method to set both status enum and string for backward compatibility
        public void SetStatus(UmcpStatus newStatus, string errorDetails = null)
        {
            status = newStatus;

            if (newStatus == UmcpStatus.Error && !string.IsNullOrEmpty(errorDetails))
            {
                configStatus = $"Error: {errorDetails}";
            }
            else
            {
                configStatus = GetStatusDisplayString();
            }
        }
    }
}