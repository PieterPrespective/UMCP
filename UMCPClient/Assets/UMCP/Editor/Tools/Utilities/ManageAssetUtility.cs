using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UMCP.Editor.Helpers;

namespace UMCP.Editor.Tools.Utilities
{
    /// <summary>
    /// Static utility class containing asset management processing logic.
    /// Separates business logic from data structures following functional programming principles.
    /// </summary>
    public static class ManageAssetUtility
    {
        #region Constants and Validation

        /// <summary>
        /// List of valid actions supported by the ManageAsset tool.
        /// </summary>
        public static readonly List<string> ValidActions = new List<string>
        {
            "import", "create", "modify", "delete", "duplicate",
            "move", "rename", "search", "get_info", "create_folder",
            "get_components"
        };

        /// <summary>
        /// Validates if an action is supported.
        /// </summary>
        /// <param name="action">The action to validate</param>
        /// <returns>True if action is valid, false otherwise</returns>
        public static bool IsValidAction(string action)
        {
            return !string.IsNullOrEmpty(action) && ValidActions.Contains(action.ToLower());
        }

        #endregion

        #region Asset Path Management

        /// <summary>
        /// Ensures the asset path starts with "Assets/" and normalizes path separators.
        /// </summary>
        /// <param name="path">The path to sanitize</param>
        /// <returns>Sanitized path starting with "Assets/"</returns>
        public static string SanitizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + path.TrimStart('/');
            }
            return path;
        }

        /// <summary>
        /// Checks if an asset exists at the given path (file or folder).
        /// </summary>
        /// <param name="sanitizedPath">The sanitized asset path</param>
        /// <returns>True if asset exists, false otherwise</returns>
        public static bool AssetExists(string sanitizedPath)
        {

            Debug.Log($"[ManageAssetUtility.AssetExists] Checking existence of asset at path: {sanitizedPath}");

            //Unfortunately AssetPathToGUID returns a guid regardless of whether the asset exists,
            //if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sanitizedPath)))
            //{
            //    Debug.Log($"[ManageAssetUtility.AssetExists] P1 {AssetDatabase.AssetPathToGUID(sanitizedPath)}");
            //    return true;
            //}

            if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                Debug.Log($"[ManageAssetUtility.AssetExists] P2");
                return AssetDatabase.IsValidFolder(sanitizedPath);
            }
            
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                FileInfo fileInfo = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath));
                Debug.Log($"[ManageAssetUtility.AssetExists] File exists: {fileInfo.FullName}, Size: {fileInfo.Length} bytes, Last Modified: {fileInfo.LastWriteTimeUtc}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ensures the directory for a given asset path exists, creating it if necessary using Unity AssetDatabase.
        /// </summary>
        /// <param name="directoryPath">The directory path to ensure exists</param>
        public static void EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) return;
            
            // For Unity assets, we need to use AssetDatabase.CreateFolder instead of Directory.CreateDirectory
            if (!AssetDatabase.IsValidFolder(directoryPath))
            {
                // Create parent directories recursively if needed
                string[] pathParts = directoryPath.Split('/');
                string currentPath = pathParts[0]; // Start with "Assets"
                
                for (int i = 1; i < pathParts.Length; i++)
                {
                    string nextPath = currentPath + "/" + pathParts[i];
                    
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        string guid = AssetDatabase.CreateFolder(currentPath, pathParts[i]);
                        if (string.IsNullOrEmpty(guid))
                        {
                            Debug.LogError($"Failed to create folder '{pathParts[i]}' in '{currentPath}'");
                            return;
                        }
                    }
                    currentPath = nextPath;
                }
            }
        }

        #endregion

        #region Asset Operations

        /// <summary>
        /// Reimports an asset with optional properties modification.
        /// </summary>
        /// <param name="path">The asset path</param>
        /// <param name="properties">Optional properties to apply before reimport</param>
        /// <returns>Response object with operation result</returns>
        public static object ReimportAsset(string path, JObject properties)
        {
            if (string.IsNullOrEmpty(path)) return Response.Error("'path' is required for reimport.");
            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath)) return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                if (properties != null && properties.HasValues)
                {
                    Debug.LogWarning("[ManageAssetUtility.ReimportAsset] Modifying importer properties before reimport is not fully implemented yet.");
                }

                AssetDatabase.ImportAsset(fullPath, ImportAssetOptions.ForceUpdate);
                return Response.Success($"Asset '{fullPath}' reimported.", GetAssetData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to reimport asset '{fullPath}': {e.Message}");
            }
        }

        /// <summary>
        /// Creates a new asset based on the specified type and parameters.
        /// </summary>
        /// <param name="createParams">Parameters for asset creation</param>
        /// <returns>Response object with operation result</returns>
        public static object CreateAsset(AssetCreationData createParams)
        {
            if (string.IsNullOrEmpty(createParams.Path)) return Response.Error("'path' is required for create.");
            if (string.IsNullOrEmpty(createParams.AssetType)) return Response.Error("'assetType' is required for create.");

            string fullPath = SanitizeAssetPath(createParams.Path);
            string directory = Path.GetDirectoryName(fullPath);

            EnsureDirectoryExists(directory);
            if (AssetExists(fullPath)) return Response.Error($"Asset already exists at path: {fullPath}");

            try
            {
                UnityEngine.Object newAsset = null;
                string lowerAssetType = createParams.AssetType.ToLowerInvariant();

                if (lowerAssetType == "folder")
                {
                    return CreateFolder(createParams.Path);
                }
                else if (lowerAssetType == "material")
                {
                    newAsset = CreateMaterialAsset(fullPath, createParams.Properties);
                }
                else if (lowerAssetType == "scriptableobject")
                {
                    newAsset = CreateScriptableObjectAsset(fullPath, createParams.Properties);
                }
                else if (lowerAssetType == "prefab")
                {
                    return Response.Error("Creating prefabs programmatically usually requires a source GameObject. Use manage_gameobject to create/configure, then save as prefab via a separate mechanism or future enhancement.");
                }
                else
                {
                    return Response.Error($"Creation for asset type '{createParams.AssetType}' is not explicitly supported yet. Supported: Folder, Material, ScriptableObject.");
                }

                if (newAsset == null && !Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), fullPath)))
                {
                    return Response.Error($"Failed to create asset '{createParams.AssetType}' at '{fullPath}'. See logs for details.");
                }

                AssetDatabase.SaveAssets();
                return Response.Success($"Asset '{fullPath}' created successfully.", GetAssetData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to create asset at '{fullPath}': {e.Message}");
            }
        }

        /// <summary>
        /// Creates a folder asset at the specified path.
        /// </summary>
        /// <param name="path">The folder path</param>
        /// <returns>Response object with operation result</returns>
        public static object CreateFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return Response.Error("'path' is required for create_folder.");
            string fullPath = SanitizeAssetPath(path);
            string parentDir = Path.GetDirectoryName(fullPath);
            string folderName = Path.GetFileName(fullPath);

            if (AssetExists(fullPath))
            {
                if (AssetDatabase.IsValidFolder(fullPath))
                {
                    return Response.Success($"Folder already exists at path: {fullPath}", GetAssetData(fullPath));
                }
                else
                {
                    return Response.Error($"An asset (not a folder) already exists at path: {fullPath}");
                }
            }

            try
            {
                string guid = AssetDatabase.CreateFolder(parentDir, folderName);
                if (string.IsNullOrEmpty(guid))
                {
                    return Response.Error($"Failed to create folder '{fullPath}'. Check logs and permissions.");
                }

                return Response.Success($"Folder '{fullPath}' created successfully.", GetAssetData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to create folder '{fullPath}': {e.Message}");
            }
        }

        /// <summary>
        /// Modifies an existing asset with the provided properties.
        /// </summary>
        /// <param name="path">The asset path</param>
        /// <param name="properties">Properties to modify</param>
        /// <returns>Response object with operation result</returns>
        public static object ModifyAsset(string path, JObject properties)
        {
            if (string.IsNullOrEmpty(path)) return Response.Error("'path' is required for modify.");
            if (properties == null || !properties.HasValues) return Response.Error("'properties' are required for modify.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath)) return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullPath);
                if (asset == null) return Response.Error($"Failed to load asset at path: {fullPath}");

                bool modified = ApplyAssetModifications(asset, properties, fullPath);

                if (modified)
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                    return Response.Success($"Asset '{fullPath}' modified successfully.", GetAssetData(fullPath));
                }
                else
                {
                    return Response.Success($"No applicable or modifiable properties found for asset '{fullPath}'. Check component names, property names, and values.", GetAssetData(fullPath));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageAssetUtility] Action 'modify' failed for path '{path}': {e}");
                return Response.Error($"Failed to modify asset '{fullPath}': {e.Message}");
            }
        }

        /// <summary>
        /// Deletes an asset at the specified path.
        /// </summary>
        /// <param name="path">The asset path</param>
        /// <returns>Response object with operation result</returns>
        public static object DeleteAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) return Response.Error("'path' is required for delete.");
            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath)) return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                bool success = AssetDatabase.DeleteAsset(fullPath);
                if (success)
                {
                    return Response.Success($"Asset '{fullPath}' deleted successfully.");
                }
                else
                {
                    return Response.Error($"Failed to delete asset '{fullPath}'. Check logs or if the file is locked.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error deleting asset '{fullPath}': {e.Message}");
            }
        }

        /// <summary>
        /// Duplicates an asset to a destination path.
        /// </summary>
        /// <param name="sourcePath">Source asset path</param>
        /// <param name="destinationPath">Destination path (optional)</param>
        /// <returns>Response object with operation result</returns>
        public static object DuplicateAsset(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrEmpty(sourcePath)) return Response.Error("'path' is required for duplicate.");

            string sanitizedSource = SanitizeAssetPath(sourcePath);
            if (!AssetExists(sanitizedSource)) return Response.Error($"Source asset not found at path: {sanitizedSource}");

            string destPath;
            if (string.IsNullOrEmpty(destinationPath))
            {
                destPath = AssetDatabase.GenerateUniqueAssetPath(sanitizedSource);
            }
            else
            {
                destPath = SanitizeAssetPath(destinationPath);
                if (AssetExists(destPath)) return Response.Error($"Asset already exists at destination path: {destPath}");
                EnsureDirectoryExists(Path.GetDirectoryName(destPath));
            }

            try
            {
                bool success = AssetDatabase.CopyAsset(sanitizedSource, destPath);
                if (success)
                {
                    return Response.Success($"Asset '{sanitizedSource}' duplicated to '{destPath}'.", GetAssetData(destPath));
                }
                else
                {
                    return Response.Error($"Failed to duplicate asset from '{sanitizedSource}' to '{destPath}'.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error duplicating asset '{sanitizedSource}': {e.Message}");
            }
        }

        /// <summary>
        /// Moves or renames an asset to a destination path.
        /// </summary>
        /// <param name="sourcePath">Source asset path</param>
        /// <param name="destinationPath">Destination path</param>
        /// <returns>Response object with operation result</returns>
        public static object MoveOrRenameAsset(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrEmpty(sourcePath)) return Response.Error("'path' is required for move/rename.");
            if (string.IsNullOrEmpty(destinationPath)) return Response.Error("'destination' path is required for move/rename.");

            string sanitizedSource = SanitizeAssetPath(sourcePath);
            string destPath = SanitizeAssetPath(destinationPath);

            if (!AssetExists(sanitizedSource)) return Response.Error($"Source asset not found at path: {sanitizedSource}");
            if (AssetExists(destPath)) return Response.Error($"An asset already exists at the destination path: {destPath}");

            EnsureDirectoryExists(Path.GetDirectoryName(destPath));

            try
            {
                string error = AssetDatabase.ValidateMoveAsset(sanitizedSource, destPath);
                if (!string.IsNullOrEmpty(error))
                {
                    return Response.Error($"Failed to move/rename asset from '{sanitizedSource}' to '{destPath}': {error}");
                }

                string guid = AssetDatabase.MoveAsset(sanitizedSource, destPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    return Response.Success($"Asset moved/renamed from '{sanitizedSource}' to '{destPath}'.", GetAssetData(destPath));
                }
                else
                {
                    return Response.Error($"MoveAsset call failed unexpectedly for '{sanitizedSource}' to '{destPath}'.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error moving/renaming asset '{sanitizedSource}': {e.Message}");
            }
        }

        #endregion

        #region Asset Search and Info

        /// <summary>
        /// Searches for assets matching the specified criteria.
        /// </summary>
        /// <param name="searchParams">Search parameters</param>
        /// <returns>Response object with search results</returns>
        public static object SearchAssets(AssetSearchData searchParams)
        {
            List<string> searchFilters = new List<string>();
            if (!string.IsNullOrEmpty(searchParams.SearchPattern)) searchFilters.Add(searchParams.SearchPattern);
            if (!string.IsNullOrEmpty(searchParams.FilterType)) searchFilters.Add($"t:{searchParams.FilterType}");

            string[] folderScope = null;
            if (!string.IsNullOrEmpty(searchParams.PathScope))
            {
                folderScope = new string[] { SanitizeAssetPath(searchParams.PathScope) };
                if (!AssetDatabase.IsValidFolder(folderScope[0]))
                {
                    Debug.LogWarning($"Search path '{folderScope[0]}' is not a valid folder. Searching entire project.");
                    folderScope = null;
                }
            }

            DateTime? filterDateAfter = null;
            if (!string.IsNullOrEmpty(searchParams.FilterDateAfter))
            {
                if (DateTime.TryParse(searchParams.FilterDateAfter, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsedDate))
                {
                    filterDateAfter = parsedDate;
                }
                else
                {
                    Debug.LogWarning($"Could not parse filterDateAfter: '{searchParams.FilterDateAfter}'. Expected ISO 8601 format.");
                }
            }

            try
            {
                string[] guids = AssetDatabase.FindAssets(string.Join(" ", searchFilters), folderScope);
                List<object> results = new List<object>();
                int totalFound = 0;

                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath)) continue;

                    if (filterDateAfter.HasValue)
                    {
                        DateTime lastWriteTime = File.GetLastWriteTimeUtc(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
                        if (lastWriteTime <= filterDateAfter.Value)
                        {
                            continue;
                        }
                    }

                    totalFound++;
                    results.Add(GetAssetData(assetPath, searchParams.GeneratePreview));
                }

                int startIndex = (searchParams.PageNumber - 1) * searchParams.PageSize;
                var pagedResults = results.Skip(startIndex).Take(searchParams.PageSize).ToList();

                return Response.Success($"Found {totalFound} asset(s). Returning page {searchParams.PageNumber} ({pagedResults.Count} assets).", new
                {
                    totalAssets = totalFound,
                    pageSize = searchParams.PageSize,
                    pageNumber = searchParams.PageNumber,
                    assets = pagedResults
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Error searching assets: {e.Message}");
            }
        }

        /// <summary>
        /// Gets information about an asset at the specified path.
        /// </summary>
        /// <param name="path">The asset path</param>
        /// <param name="generatePreview">Whether to generate a preview</param>
        /// <returns>Response object with asset information</returns>
        public static object GetAssetInfo(string path, bool generatePreview)
        {
            if (string.IsNullOrEmpty(path)) return Response.Error("'path' is required for get_info.");
            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath)) return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                return Response.Success("Asset info retrieved.", GetAssetData(fullPath, generatePreview));
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting info for asset '{fullPath}': {e.Message}");
            }
        }

        /// <summary>
        /// Retrieves components attached to a GameObject asset (like a Prefab).
        /// </summary>
        /// <param name="path">The asset path of the GameObject or Prefab</param>
        /// <returns>Response object containing component information</returns>
        public static object GetComponentsFromAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) return Response.Error("'path' is required for get_components.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath)) return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullPath);
                if (asset == null) return Response.Error($"Failed to load asset at path: {fullPath}");

                GameObject gameObject = asset as GameObject;
                if (gameObject == null)
                {
                    Component componentAsset = asset as Component;
                    if (componentAsset != null)
                    {
                        return Response.Error($"Asset at '{fullPath}' is a Component ({asset.GetType().FullName}), not a GameObject. Components are typically retrieved *from* a GameObject.");
                    }
                    return Response.Error($"Asset at '{fullPath}' is not a GameObject (Type: {asset.GetType().FullName}). Cannot get components from this asset type.");
                }

                Component[] components = gameObject.GetComponents<Component>();

                List<object> componentList = components.Select(comp => new
                {
                    typeName = comp.GetType().FullName,
                    instanceID = comp.GetInstanceID(),
                }).ToList<object>();

                return Response.Success($"Found {componentList.Count} component(s) on asset '{fullPath}'.", componentList);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageAssetUtility.GetComponentsFromAsset] Error getting components for '{fullPath}': {e}");
                return Response.Error($"Error getting components for asset '{fullPath}': {e.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private static Material CreateMaterialAsset(string fullPath, JObject properties)
        {
            Material mat = new Material(Shader.Find("Standard"));
            if (properties != null) ApplyMaterialProperties(mat, properties);
            AssetDatabase.CreateAsset(mat, fullPath);
            return mat;
        }

        private static ScriptableObject CreateScriptableObjectAsset(string fullPath, JObject properties)
        {
            string scriptClassName = properties?["scriptClass"]?.ToString();
            if (string.IsNullOrEmpty(scriptClassName))
                throw new ArgumentException("'scriptClass' property required when creating ScriptableObject asset.");

            Type scriptType = ReflectionUtility.FindType(scriptClassName);
            if (scriptType == null || !typeof(ScriptableObject).IsAssignableFrom(scriptType))
            {
                throw new ArgumentException($"Script class '{scriptClassName}' not found or does not inherit from ScriptableObject.");
            }

            ScriptableObject so = ScriptableObject.CreateInstance(scriptType);
            AssetDatabase.CreateAsset(so, fullPath);
            return so;
        }

        private static bool ApplyAssetModifications(UnityEngine.Object asset, JObject properties, string fullPath)
        {
            bool modified = false;

            if (asset is GameObject gameObject)
            {
                modified = ApplyGameObjectModifications(gameObject, properties);
            }
            else if (asset is Material material)
            {
                modified = ApplyMaterialProperties(material, properties);
            }
            else if (asset is ScriptableObject so)
            {
                modified = PropertyUtility.ApplyObjectProperties(so, properties);
            }
            else if (asset is Texture)
            {
                modified = ApplyTextureImporterModifications(fullPath, properties);
            }
            else
            {
                Debug.LogWarning($"[ManageAssetUtility.ApplyAssetModifications] Asset type '{asset.GetType().Name}' at '{fullPath}' is not explicitly handled for component modification. Attempting generic property setting on the asset itself.");
                modified = PropertyUtility.ApplyObjectProperties(asset, properties);
            }

            return modified;
        }

        private static bool ApplyGameObjectModifications(GameObject gameObject, JObject properties)
        {
            bool modified = false;

            foreach (var prop in properties.Properties())
            {
                string componentName = prop.Name;
                if (prop.Value is JObject componentProperties && componentProperties.HasValues)
                {
                    Component targetComponent = gameObject.GetComponent(componentName);
                    if (targetComponent != null)
                    {
                        modified |= PropertyUtility.ApplyObjectProperties(targetComponent, componentProperties);
                    }
                    else
                    {
                        Debug.LogWarning($"[ManageAssetUtility.ApplyGameObjectModifications] Component '{componentName}' not found on GameObject '{gameObject.name}'. Skipping modification for this component.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ManageAssetUtility.ApplyGameObjectModifications] Property '{prop.Name}' for GameObject modification should have a JSON object value containing component properties. Value was: {prop.Value.Type}. Skipping.");
                }
            }

            return modified;
        }

        private static bool ApplyTextureImporterModifications(string fullPath, JObject properties)
        {
            AssetImporter importer = AssetImporter.GetAtPath(fullPath);
            if (importer is TextureImporter textureImporter)
            {
                bool importerModified = PropertyUtility.ApplyObjectProperties(textureImporter, properties);
                if (importerModified)
                {
                    AssetDatabase.WriteImportSettingsIfDirty(fullPath);
                    AssetDatabase.ImportAsset(fullPath, ImportAssetOptions.ForceUpdate);
                    return true;
                }
            }
            else
            {
                Debug.LogWarning($"Could not get TextureImporter for {fullPath}.");
            }
            return false;
        }

        private static bool ApplyMaterialProperties(Material mat, JObject properties)
        {
            if (mat == null || properties == null) return false;
            bool modified = false;

            if (properties["shader"]?.Type == JTokenType.String)
            {
                Shader newShader = Shader.Find(properties["shader"].ToString());
                if (newShader != null && mat.shader != newShader)
                {
                    mat.shader = newShader;
                    modified = true;
                }
            }

            if (properties["color"] is JObject colorProps)
            {
                modified |= ApplyMaterialColorProperty(mat, colorProps);
            }

            if (properties["float"] is JObject floatProps)
            {
                modified |= ApplyMaterialFloatProperty(mat, floatProps);
            }

            if (properties["texture"] is JObject texProps)
            {
                modified |= ApplyMaterialTextureProperty(mat, texProps);
            }

            return modified;
        }

        private static bool ApplyMaterialColorProperty(Material mat, JObject colorProps)
        {
            string propName = colorProps["name"]?.ToString() ?? "_Color";
            if (colorProps["value"] is JArray colArr && colArr.Count >= 3)
            {
                try
                {
                    Color newColor = new Color(
                        colArr[0].ToObject<float>(),
                        colArr[1].ToObject<float>(),
                        colArr[2].ToObject<float>(),
                        colArr.Count > 3 ? colArr[3].ToObject<float>() : 1.0f
                    );
                    if (mat.HasProperty(propName) && mat.GetColor(propName) != newColor)
                    {
                        mat.SetColor(propName, newColor);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error parsing color property '{propName}': {ex.Message}");
                }
            }
            return false;
        }

        private static bool ApplyMaterialFloatProperty(Material mat, JObject floatProps)
        {
            string propName = floatProps["name"]?.ToString();
            if (!string.IsNullOrEmpty(propName) && (floatProps["value"]?.Type == JTokenType.Float || floatProps["value"]?.Type == JTokenType.Integer))
            {
                try
                {
                    float newVal = floatProps["value"].ToObject<float>();
                    if (mat.HasProperty(propName) && mat.GetFloat(propName) != newVal)
                    {
                        mat.SetFloat(propName, newVal);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error parsing float property '{propName}': {ex.Message}");
                }
            }
            return false;
        }

        private static bool ApplyMaterialTextureProperty(Material mat, JObject texProps)
        {
            string propName = texProps["name"]?.ToString() ?? "_MainTex";
            string texPath = texProps["path"]?.ToString();
            if (!string.IsNullOrEmpty(texPath))
            {
                Texture newTex = AssetDatabase.LoadAssetAtPath<Texture>(SanitizeAssetPath(texPath));
                if (newTex != null && mat.HasProperty(propName) && mat.GetTexture(propName) != newTex)
                {
                    mat.SetTexture(propName, newTex);
                    return true;
                }
                else if (newTex == null)
                {
                    Debug.LogWarning($"Texture not found at path: {texPath}");
                }
            }
            return false;
        }

        /// <summary>
        /// Creates a serializable representation of an asset.
        /// </summary>
        /// <param name="path">The asset path</param>
        /// <param name="generatePreview">Whether to generate a preview</param>
        /// <returns>Asset data object</returns>
        public static object GetAssetData(string path, bool generatePreview = false)
        {
            if (string.IsNullOrEmpty(path) || !AssetExists(path)) return null;

            string guid = AssetDatabase.AssetPathToGUID(path);
            Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            
            var previewData = generatePreview && asset != null ? 
                GenerateAssetPreview(asset) : 
                new AssetPreviewData();

            return new
            {
                path = path,
                guid = guid,
                assetType = assetType?.FullName ?? "Unknown",
                name = Path.GetFileNameWithoutExtension(path),
                fileName = Path.GetFileName(path),
                isFolder = AssetDatabase.IsValidFolder(path),
                instanceID = asset?.GetInstanceID() ?? 0,
                lastWriteTimeUtc = File.GetLastWriteTimeUtc(Path.Combine(Directory.GetCurrentDirectory(), path)).ToString("o"),
                previewBase64 = previewData.Base64Data,
                previewWidth = previewData.Width,
                previewHeight = previewData.Height
            };
        }

        private static AssetPreviewData GenerateAssetPreview(UnityEngine.Object asset)
        {
            Texture2D preview = AssetPreview.GetAssetPreview(asset);
            if (preview == null)
            {
                Debug.LogWarning($"Could not get asset preview for {asset.name} (Type: {asset.GetType().Name}). Is it supported?");
                return new AssetPreviewData();
            }

            try
            {
                RenderTexture rt = RenderTexture.GetTemporary(preview.width, preview.height);
                Graphics.Blit(preview, rt);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D readablePreview = new Texture2D(preview.width, preview.height);
                readablePreview.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                readablePreview.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                byte[] pngData = readablePreview.EncodeToPNG();
                string base64Data = Convert.ToBase64String(pngData);
                int width = readablePreview.width;
                int height = readablePreview.height;
                
                UnityEngine.Object.DestroyImmediate(readablePreview);

                return new AssetPreviewData { Base64Data = base64Data, Width = width, Height = height };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to generate readable preview: {ex.Message}. Preview might not be readable.");
                return new AssetPreviewData();
            }
        }

        #endregion
    }

    #region Data Structures

    /// <summary>
    /// Data structure for asset creation parameters.
    /// </summary>
    public struct AssetCreationData
    {
        public string Path { get; set; }
        public string AssetType { get; set; }
        public JObject Properties { get; set; }
    }

    /// <summary>
    /// Data structure for asset search parameters.
    /// </summary>
    public struct AssetSearchData
    {
        public string SearchPattern { get; set; }
        public string FilterType { get; set; }
        public string PathScope { get; set; }
        public string FilterDateAfter { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public bool GeneratePreview { get; set; }
    }

    /// <summary>
    /// Data structure for asset preview information.
    /// </summary>
    public struct AssetPreviewData
    {
        public string Base64Data { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    #endregion
}