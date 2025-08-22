using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UMCP.Editor.Helpers;

namespace UMCP.Editor.Tools.Utilities
{
    /// <summary>
    /// Static utility class containing GameObject management processing logic.
    /// Separates business logic from data structures following functional programming principles.
    /// </summary>
    public static class ManageGameObjectUtility
    {
        #region Constants and Validation

        /// <summary>
        /// List of valid actions supported by the ManageGameObject tool.
        /// </summary>
        public static readonly List<string> ValidActions = new List<string>
        {
            "create", "modify", "delete", "find",
            "get_components", "add_component", "remove_component", "set_component_property"
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

        #region GameObject Finding

        /// <summary>
        /// Finds a GameObject using various search methods.
        /// </summary>
        /// <param name="targetToken">The target identifier (name, path, or instance ID)</param>
        /// <param name="searchMethod">The search method to use</param>
        /// <returns>The found GameObject or null</returns>
        public static GameObject FindGameObject(JToken targetToken, string searchMethod)
        {
            if (targetToken == null) return null;

            searchMethod = searchMethod?.ToLower() ?? "auto";

            // Handle instance ID
            if (targetToken.Type == JTokenType.Integer || 
                (targetToken.Type == JTokenType.String && int.TryParse(targetToken.ToString(), out _)))
            {
                int instanceId = targetToken.ToObject<int>();
                return EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            }

            string target = targetToken.ToString();
            if (string.IsNullOrEmpty(target)) return null;

            return searchMethod switch
            {
                "name" => GameObject.Find(target),
                "path" => FindGameObjectByPath(target),
                "tag" => FindGameObjectByTag(target),
                "type" => FindGameObjectByComponentType(target),
                "auto" => FindGameObjectAuto(target),
                _ => FindGameObjectAuto(target)
            };
        }

        /// <summary>
        /// Finds multiple GameObjects based on search criteria.
        /// </summary>
        /// <param name="searchData">Search parameters</param>
        /// <returns>List of found GameObjects</returns>
        public static List<GameObject> FindGameObjects(GameObjectSearchData searchData)
        {
            List<GameObject> results = new List<GameObject>();

            if (!string.IsNullOrEmpty(searchData.Name))
            {
                var obj = GameObject.Find(searchData.Name);
                if (obj != null) results.Add(obj);
            }

            if (!string.IsNullOrEmpty(searchData.Tag))
            {
                results.AddRange(GameObject.FindGameObjectsWithTag(searchData.Tag));
            }

            if (searchData.Layer >= 0)
            {
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                results.AddRange(allObjects.Where(go => go.layer == searchData.Layer));
            }

            if (!string.IsNullOrEmpty(searchData.ComponentType))
            {
                Type componentType = ReflectionUtility.FindType(searchData.ComponentType);
                if (componentType != null)
                {
                    var components = UnityEngine.Object.FindObjectsOfType(componentType);
                    foreach (Component comp in components)
                    {
                        if (comp != null && comp.gameObject != null)
                            results.Add(comp.gameObject);
                    }
                }
            }

            // Remove duplicates
            return results.Distinct().Take(searchData.MaxResults).ToList();
        }

        private static GameObject FindGameObjectByPath(string path)
        {
            if (path.Contains("/"))
            {
                return GameObject.Find(path);
            }
            return null;
        }

        private static GameObject FindGameObjectByTag(string tag)
        {
            try
            {
                return GameObject.FindWithTag(tag);
            }
            catch
            {
                return null;
            }
        }

        private static GameObject FindGameObjectByComponentType(string typeName)
        {
            Type type = ReflectionUtility.FindType(typeName);
            if (type != null)
            {
                var component = UnityEngine.Object.FindObjectOfType(type) as Component;
                return component?.gameObject;
            }
            return null;
        }

        private static GameObject FindGameObjectAuto(string target)
        {
            // Try by name/path first
            GameObject result = GameObject.Find(target);
            if (result != null) return result;

            // Try by tag
            result = FindGameObjectByTag(target);
            if (result != null) return result;

            // Try by component type
            result = FindGameObjectByComponentType(target);
            return result;
        }

        #endregion

        #region GameObject Creation

        /// <summary>
        /// Creates a new GameObject based on the provided parameters.
        /// </summary>
        /// <param name="creationData">GameObject creation parameters</param>
        /// <returns>Response object with operation result</returns>
        public static object CreateGameObject(GameObjectCreationData creationData)
        {
            if (string.IsNullOrEmpty(creationData.Name))
            {
                return Response.Error("'name' parameter is required for 'create' action.");
            }

            GameObject newGo = null;

            try
            {
                // Try to instantiate from prefab if path provided
                if (!string.IsNullOrEmpty(creationData.PrefabPath))
                {
                    newGo = InstantiatePrefab(creationData.PrefabPath, creationData.Name);
                    if (newGo == null)
                    {
                        return Response.Error($"Failed to instantiate prefab from path: {creationData.PrefabPath}");
                    }
                }
                // Create primitive if specified
                else if (!string.IsNullOrEmpty(creationData.PrimitiveType))
                {
                    newGo = CreatePrimitive(creationData.PrimitiveType);
                    if (newGo == null)
                    {
                        return Response.Error($"Invalid primitive type: {creationData.PrimitiveType}");
                    }
                    newGo.name = creationData.Name;
                }
                // Create empty GameObject
                else
                {
                    newGo = new GameObject(creationData.Name);
                }

                // Set parent if specified
                if (creationData.Parent != null)
                {
                    GameObject parent = FindGameObject(creationData.Parent, creationData.SearchMethod);
                    if (parent != null)
                    {
                        newGo.transform.SetParent(parent.transform);
                    }
                }

                // Set transform properties
                ApplyTransformData(newGo, creationData.Transform);

                // Set tag if specified
                if (!string.IsNullOrEmpty(creationData.Tag))
                {
                    try
                    {
                        newGo.tag = creationData.Tag;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to set tag '{creationData.Tag}': {ex.Message}");
                    }
                }

                // Set layer if specified
                if (creationData.Layer >= 0)
                {
                    newGo.layer = creationData.Layer;
                }

                // Add components if specified
                foreach (string componentType in creationData.ComponentsToAdd)
                {
                    AddComponent(newGo, componentType);
                }

                // Mark scene as dirty
                EditorSceneManager.MarkSceneDirty(newGo.scene);

                return Response.Success($"GameObject '{newGo.name}' created successfully.", GetGameObjectData(newGo));
            }
            catch (Exception e)
            {
                if (newGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(newGo);
                }
                return Response.Error($"Failed to create GameObject: {e.Message}");
            }
        }

        private static GameObject InstantiatePrefab(string prefabPath, string instanceName)
        {
            // Ensure path has .prefab extension
            if (!prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                prefabPath += ".prefab";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = "Assets/" + prefabPath.TrimStart('/');
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null) return null;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            if (instance != null && !string.IsNullOrEmpty(instanceName))
            {
                instance.name = instanceName;
            }

            return instance;
        }

        private static GameObject CreatePrimitive(string primitiveType)
        {
            if (Enum.TryParse<PrimitiveType>(primitiveType, true, out PrimitiveType type))
            {
                return GameObject.CreatePrimitive(type);
            }
            return null;
        }

        #endregion

        #region GameObject Modification

        /// <summary>
        /// Modifies a GameObject with the provided properties.
        /// </summary>
        /// <param name="gameObject">The GameObject to modify</param>
        /// <param name="modificationData">Modification parameters</param>
        /// <returns>Response object with operation result</returns>
        public static object ModifyGameObject(GameObject gameObject, GameObjectModificationData modificationData)
        {
            if (gameObject == null)
            {
                return Response.Error("GameObject not found.");
            }

            try
            {
                bool modified = false;

                // Update name
                if (!string.IsNullOrEmpty(modificationData.Name) && gameObject.name != modificationData.Name)
                {
                    Undo.RecordObject(gameObject, "Rename GameObject");
                    gameObject.name = modificationData.Name;
                    modified = true;
                }

                // Update tag
                if (!string.IsNullOrEmpty(modificationData.Tag) && gameObject.tag != modificationData.Tag)
                {
                    Undo.RecordObject(gameObject, "Change GameObject Tag");
                    gameObject.tag = modificationData.Tag;
                    modified = true;
                }

                // Update layer
                if (modificationData.Layer >= 0 && gameObject.layer != modificationData.Layer)
                {
                    Undo.RecordObject(gameObject, "Change GameObject Layer");
                    gameObject.layer = modificationData.Layer;
                    modified = true;
                }

                // Update active state
                if (modificationData.IsActive.HasValue && gameObject.activeSelf != modificationData.IsActive.Value)
                {
                    Undo.RecordObject(gameObject, "Change GameObject Active State");
                    gameObject.SetActive(modificationData.IsActive.Value);
                    modified = true;
                }

                // Update transform
                if (modificationData.Transform != null)
                {
                    Undo.RecordObject(gameObject.transform, "Modify Transform");
                    ApplyTransformData(gameObject, modificationData.Transform);
                    modified = true;
                }

                // Update parent
                if (modificationData.Parent != null)
                {
                    GameObject newParent = FindGameObject(modificationData.Parent, modificationData.SearchMethod);
                    if (newParent != null && gameObject.transform.parent != newParent.transform)
                    {
                        Undo.SetTransformParent(gameObject.transform, newParent.transform, "Change Parent");
                        modified = true;
                    }
                }

                if (modified)
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                    return Response.Success($"GameObject '{gameObject.name}' modified successfully.", GetGameObjectData(gameObject));
                }
                else
                {
                    return Response.Success("No changes were made to the GameObject.", GetGameObjectData(gameObject));
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to modify GameObject: {e.Message}");
            }
        }

        #endregion

        #region GameObject Deletion

        /// <summary>
        /// Deletes a GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to delete</param>
        /// <returns>Response object with operation result</returns>
        public static object DeleteGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return Response.Error("GameObject not found.");
            }

            try
            {
                string name = gameObject.name;
                Scene scene = gameObject.scene;
                
                Undo.DestroyObjectImmediate(gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
                
                return Response.Success($"GameObject '{name}' deleted successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to delete GameObject: {e.Message}");
            }
        }

        #endregion

        #region Component Management

        /// <summary>
        /// Adds a component to a GameObject.
        /// </summary>
        /// <param name="gameObject">The target GameObject</param>
        /// <param name="componentTypeName">The type name of the component to add</param>
        /// <returns>Response object with operation result</returns>
        public static object AddComponent(GameObject gameObject, string componentTypeName)
        {
            if (gameObject == null)
            {
                return Response.Error("GameObject not found.");
            }

            if (string.IsNullOrEmpty(componentTypeName))
            {
                return Response.Error("Component type name is required.");
            }

            try
            {
                Type componentType = ReflectionUtility.FindType(componentTypeName);
                if (componentType == null)
                {
                    return Response.Error($"Component type '{componentTypeName}' not found.");
                }

                if (!typeof(Component).IsAssignableFrom(componentType))
                {
                    return Response.Error($"Type '{componentTypeName}' is not a Component.");
                }

                Component newComponent = Undo.AddComponent(gameObject, componentType);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);

                return Response.Success($"Component '{componentType.Name}' added to GameObject '{gameObject.name}'.", 
                    GetComponentData(newComponent));
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to add component: {e.Message}");
            }
        }

        /// <summary>
        /// Removes a component from a GameObject.
        /// </summary>
        /// <param name="gameObject">The target GameObject</param>
        /// <param name="componentTypeName">The type name of the component to remove</param>
        /// <returns>Response object with operation result</returns>
        public static object RemoveComponent(GameObject gameObject, string componentTypeName)
        {
            if (gameObject == null)
            {
                return Response.Error("GameObject not found.");
            }

            if (string.IsNullOrEmpty(componentTypeName))
            {
                return Response.Error("Component type name is required.");
            }

            try
            {
                Type componentType = ReflectionUtility.FindType(componentTypeName);
                if (componentType == null)
                {
                    return Response.Error($"Component type '{componentTypeName}' not found.");
                }

                Component component = gameObject.GetComponent(componentType);
                if (component == null)
                {
                    return Response.Error($"Component '{componentTypeName}' not found on GameObject '{gameObject.name}'.");
                }

                // Cannot remove Transform component
                if (component is Transform)
                {
                    return Response.Error("Cannot remove Transform component.");
                }

                string componentName = component.GetType().Name;
                Undo.DestroyObjectImmediate(component);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);

                return Response.Success($"Component '{componentName}' removed from GameObject '{gameObject.name}'.");
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to remove component: {e.Message}");
            }
        }

        /// <summary>
        /// Sets properties on a component.
        /// </summary>
        /// <param name="gameObject">The target GameObject</param>
        /// <param name="componentData">Component property data</param>
        /// <returns>Response object with operation result</returns>
        public static object SetComponentProperty(GameObject gameObject, ComponentPropertyData componentData)
        {
            if (gameObject == null)
            {
                return Response.Error("GameObject not found.");
            }

            if (string.IsNullOrEmpty(componentData.ComponentTypeName))
            {
                return Response.Error("Component type name is required.");
            }

            if (componentData.Properties == null || !componentData.Properties.HasValues)
            {
                return Response.Error("Component properties are required.");
            }

            try
            {
                Type componentType = ReflectionUtility.FindType(componentData.ComponentTypeName);
                if (componentType == null)
                {
                    return Response.Error($"Component type '{componentData.ComponentTypeName}' not found.");
                }

                Component component = gameObject.GetComponent(componentType);
                if (component == null)
                {
                    return Response.Error($"Component '{componentData.ComponentTypeName}' not found on GameObject '{gameObject.name}'.");
                }

                Undo.RecordObject(component, $"Modify {componentType.Name} Properties");
                bool modified = PropertyUtility.ApplyObjectProperties(component, componentData.Properties);

                if (modified)
                {
                    EditorUtility.SetDirty(component);
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                    return Response.Success($"Component '{componentType.Name}' properties updated on GameObject '{gameObject.name}'.", 
                        GetComponentData(component));
                }
                else
                {
                    return Response.Success("No changes were made to the component properties.", 
                        GetComponentData(component));
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to set component property: {e.Message}");
            }
        }

        /// <summary>
        /// Gets all components from a GameObject.
        /// </summary>
        /// <param name="gameObject">The target GameObject</param>
        /// <returns>Response object with component list</returns>
        public static object GetComponents(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return Response.Error("GameObject not found.");
            }

            try
            {
                Component[] components = gameObject.GetComponents<Component>();
                List<object> componentList = components.Select(GetComponentData).ToList();

                return Response.Success($"Found {componentList.Count} component(s) on GameObject '{gameObject.name}'.", componentList);
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to get components: {e.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private static void ApplyTransformData(GameObject gameObject, TransformData transformData)
        {
            if (transformData == null || gameObject == null) return;

            Transform transform = gameObject.transform;

            if (transformData.Position.HasValue)
                transform.localPosition = transformData.Position.Value;

            if (transformData.Rotation.HasValue)
                transform.localRotation = transformData.Rotation.Value;

            if (transformData.Scale.HasValue)
                transform.localScale = transformData.Scale.Value;
        }

        private static object GetGameObjectData(GameObject gameObject)
        {
            if (gameObject == null) return null;

            return new
            {
                name = gameObject.name,
                instanceID = gameObject.GetInstanceID(),
                tag = gameObject.tag,
                layer = gameObject.layer,
                layerName = LayerMask.LayerToName(gameObject.layer),
                isActive = gameObject.activeSelf,
                isStatic = gameObject.isStatic,
                scene = gameObject.scene.name,
                transform = new
                {
                    position = gameObject.transform.position,
                    localPosition = gameObject.transform.localPosition,
                    rotation = gameObject.transform.rotation.eulerAngles,
                    localRotation = gameObject.transform.localRotation.eulerAngles,
                    scale = gameObject.transform.localScale,
                    parent = gameObject.transform.parent?.name,
                    childCount = gameObject.transform.childCount
                },
                componentCount = gameObject.GetComponents<Component>().Length,
                isPrefab = PrefabUtility.IsPartOfPrefabInstance(gameObject)
            };
        }

        private static object GetComponentData(Component component)
        {
            if (component == null) return null;

            return new
            {
                typeName = component.GetType().FullName,
                instanceID = component.GetInstanceID(),
                gameObject = component.gameObject.name,
                enabled = (component is Behaviour behaviour) ? behaviour.enabled : true
            };
        }

        #endregion
    }

    #region Data Structures

    /// <summary>
    /// Data structure for GameObject creation parameters.
    /// </summary>
    public struct GameObjectCreationData
    {
        public string Name { get; set; }
        public string PrefabPath { get; set; }
        public string PrimitiveType { get; set; }
        public JToken Parent { get; set; }
        public string SearchMethod { get; set; }
        public TransformData Transform { get; set; }
        public string Tag { get; set; }
        public int Layer { get; set; }
        public List<string> ComponentsToAdd { get; set; }
    }

    /// <summary>
    /// Data structure for GameObject modification parameters.
    /// </summary>
    public struct GameObjectModificationData
    {
        public string Name { get; set; }
        public string Tag { get; set; }
        public int Layer { get; set; }
        public bool? IsActive { get; set; }
        public TransformData Transform { get; set; }
        public JToken Parent { get; set; }
        public string SearchMethod { get; set; }
    }

    /// <summary>
    /// Data structure for GameObject search parameters.
    /// </summary>
    public struct GameObjectSearchData
    {
        public string Name { get; set; }
        public string Tag { get; set; }
        public int Layer { get; set; }
        public string ComponentType { get; set; }
        public int MaxResults { get; set; }
    }

    /// <summary>
    /// Data structure for Transform properties.
    /// </summary>
    public class TransformData
    {
        public Vector3? Position { get; set; }
        public Quaternion? Rotation { get; set; }
        public Vector3? Scale { get; set; }
    }

    /// <summary>
    /// Data structure for component property modification.
    /// </summary>
    public struct ComponentPropertyData
    {
        public string ComponentTypeName { get; set; }
        public JObject Properties { get; set; }
    }

    #endregion
}