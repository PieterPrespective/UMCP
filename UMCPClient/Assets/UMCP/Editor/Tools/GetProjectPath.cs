using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using UMCP.Editor.Helpers; // For Response class

namespace UMCP.Editor.Tools
{
    /// <summary>
    /// Handles project path operations within the Unity project.
    /// </summary>
    public static class GetProjectPath
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string projectPath = Application.dataPath;
                // Remove "Assets" from the end since Application.dataPath includes it
                projectPath = projectPath.Substring(0, projectPath.Length - 7);

                return Response.Success("Project path retrieved successfully.", new
                {
                    projectPath = projectPath,
                    dataPath = Application.dataPath,
                    persistentDataPath = Application.persistentDataPath,
                    streamingAssetsPath = Application.streamingAssetsPath,
                    temporaryCachePath = Application.temporaryCachePath
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[GetProjectPath] Failed to get project path: {e}");
                return Response.Error($"Failed to get project path: {e.Message}");
            }
        }
    }
}