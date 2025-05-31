using System;
using System.Collections.Generic;
using System.IO;
using UMCP.Editor.Models;

namespace UMCP.Editor.Data
{
    public class UmcpClients
    {
        public List<UmcpClient> clients = new() {
            new() {
                name = "Claude Desktop",
                windowsConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Claude",
                    "claude_desktop_config.json"
                ),
                linuxConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library",
                    "Application Support",
                    "Claude",
                    "claude_desktop_config.json"
                ),
                umcpType = UmcpTypes.ClaudeDesktop,
                configStatus = "Not Configured"
            },
            new() {
                name = "Cursor",
                windowsConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".cursor",
                    "mcp.json"
                ),
                linuxConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".cursor",
                    "mcp.json"
                ),
                umcpType = UmcpTypes.Cursor,
                configStatus = "Not Configured"
            },
            new() {
                name = "VS Code",
                windowsConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".vscode",
                    "mcp.json"
                ),
                linuxConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".vscode",
                    "mcp.json"
                ),
                umcpType = UmcpTypes.VSCode,
                configStatus = "Not Configured"
            }
        };

        // Initialize status enums after construction
        public UmcpClients()
        {
            foreach (var client in clients)
            {
                if (client.configStatus == "Not Configured")
                {
                    client.status = UmcpStatus.NotConfigured;
                }
            }
        }
    }
}