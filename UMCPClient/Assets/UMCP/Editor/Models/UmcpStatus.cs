namespace UMCP.Editor.Models
{
    // Enum representing the various status states for UMCP clients
    public enum UmcpStatus
    {
        NotConfigured,  // Not set up yet
        Configured,     // Successfully configured
        Running,        // Service is running
        Connected,      // Successfully connected
        IncorrectPath,  // Configuration has incorrect paths
        CommunicationError, // Connected but communication issues
        NoResponse,     // Connected but not responding
        MissingConfig,  // Config file exists but missing required elements
        UnsupportedOS,  // OS is not supported
        Error           // General error state
    }
}