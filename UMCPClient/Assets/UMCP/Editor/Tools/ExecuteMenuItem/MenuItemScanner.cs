namespace UMCP.Editor.Tools
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Utility class to scan and retrieve all MenuItem attributes in the Unity project
    /// </summary>
    public static class MenuItemScanner
    {
        [System.Serializable]
        [JsonObject(MemberSerialization.OptIn)]
        public class MenuItemInfo
        {
            [JsonProperty]
            public string menuPath;

            [JsonProperty]
            public string methodName;

            [JsonProperty]
            public string className;

            [JsonProperty]
            public string assemblyName;

            [JsonProperty]
            public bool isValidateFunction;

            [JsonProperty]
            public int priority;

            [JsonProperty]
            public string shortcut;

            public override string ToString()
            {
                return $"{menuPath} [{className}.{methodName}] Priority: {priority}";
            }
        }

        /// <summary>
        /// Retrieves all MenuItem attributes from all loaded assemblies
        /// </summary>
        public static List<MenuItemInfo> GetAllMenuItems(string _filter = null)
        {
            Debug.Log("Scanning for MenuItem attributes with filter" + _filter + "...");

            var menuItems = new List<MenuItemInfo>();

            // Get all loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    // Skip system assemblies for performance
                    if (assembly.FullName.StartsWith("System") ||
                        assembly.FullName.StartsWith("mscorlib") ||
                        assembly.FullName.StartsWith("netstandard"))
                        continue;

                    // Get all types in the assembly
                    var types = assembly.GetTypes();

                    foreach (var type in types)
                    {
                        // Get all methods (static and instance, public and non-public)
                        var methods = type.GetMethods(BindingFlags.Static |
                                                     BindingFlags.Public |
                                                     BindingFlags.NonPublic);

                        foreach (var method in methods)
                        {
                            // Check if method has MenuItem attribute
                            var menuItemAttrs = method.GetCustomAttributes(typeof(MenuItem), false);

                            if (menuItemAttrs.Length > 0)
                            {
                                var attr = menuItemAttrs[0] as MenuItem;
                                if (attr != null)
                                {
                                    var info = new MenuItemInfo
                                    {
                                        menuPath = attr.menuItem,
                                        methodName = method.Name,
                                        className = type.FullName,
                                        assemblyName = assembly.GetName().Name,
                                        isValidateFunction = attr.validate,
                                        priority = attr.priority
                                    };

                                    // Parse shortcut from menu path if present
                                    if (info.menuPath.Contains(" "))
                                    {
                                        var parts = info.menuPath.Split(' ');
                                        if (parts.Length > 1)
                                        {
                                            info.shortcut = string.Join(" ", parts.Skip(1));
                                            info.menuPath = parts[0];
                                        }
                                    }

                                    menuItems.Add(info);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // Some assemblies might not be accessible
                    Debug.LogWarning($"Could not scan assembly {assembly.FullName}: {e.Message}");
                }
            }

            // Sort by menu path for better organization
            
            if(!string.IsNullOrEmpty(_filter))
            {
                string toLowerFilter = _filter?.ToLower();
                List<MenuItemInfo> filteredItems = menuItems.Where((item) => item.menuPath.ToLower().Contains(toLowerFilter)).ToList();
                menuItems = filteredItems;
            }

            menuItems.Sort((a, b) => string.Compare(a.menuPath, b.menuPath, StringComparison.Ordinal));

            return menuItems;
        }

        /// <summary>
        /// Get menu items organized by their top-level menu
        /// </summary>
        public static Dictionary<string, List<MenuItemInfo>> GetMenuItemsByCategory()
        {
            var menuItems = GetAllMenuItems();
            var categorized = new Dictionary<string, List<MenuItemInfo>>();

            foreach (var item in menuItems)
            {
                var parts = item.menuPath.Split('/');
                if (parts.Length > 0)
                {
                    var category = parts[0];
                    if (!categorized.ContainsKey(category))
                    {
                        categorized[category] = new List<MenuItemInfo>();
                    }
                    categorized[category].Add(item);
                }
            }

            return categorized;
        }

        /// <summary>
        /// Alternative method using Unity's internal Menu class (if accessible)
        /// </summary>
        public static List<string> GetMenuItemsViaUnityMenu()
        {
            var menuPaths = new List<string>();

            try
            {
                // Try to access Unity's internal Menu class
                var menuType = typeof(EditorWindow).Assembly.GetType("UnityEditor.Menu");
                if (menuType != null)
                {
                    var getMenuItemsMethod = menuType.GetMethod("GetMenuItems",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                    if (getMenuItemsMethod != null)
                    {
                        var result = getMenuItemsMethod.Invoke(null, new object[] { true, false });
                        // The result format may vary depending on Unity version
                        Debug.Log($"Found Unity Menu.GetMenuItems: {result}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not access Unity's internal Menu class: {e.Message}");
            }

            return menuPaths;
        }
    }

    /// <summary>
    /// Editor window to display all discovered menu items
    /// </summary>
    public class MenuItemBrowserWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<MenuItemScanner.MenuItemInfo> menuItems;
        private Dictionary<string, List<MenuItemScanner.MenuItemInfo>> categorizedItems;
        private string searchFilter = "";
        private bool showValidateFunctions = true;
        private Dictionary<string, bool> categoryFoldouts = new Dictionary<string, bool>();

        [MenuItem("Tools/Menu Item Browser")]
        public static void ShowWindow()
        {
            var window = GetWindow<MenuItemBrowserWindow>("Menu Item Browser");
            window.RefreshMenuItems();
        }

        private void OnEnable()
        {
            RefreshMenuItems();
        }

        private void RefreshMenuItems()
        {
            menuItems = MenuItemScanner.GetAllMenuItems();
            categorizedItems = MenuItemScanner.GetMenuItemsByCategory();

            // Initialize foldouts
            foreach (var category in categorizedItems.Keys)
            {
                if (!categoryFoldouts.ContainsKey(category))
                {
                    categoryFoldouts[category] = true;
                }
            }

            Debug.Log($"Found {menuItems.Count} menu items across {categorizedItems.Count} categories");
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshMenuItems();
            }

            GUILayout.Label("Search:", GUILayout.Width(50));
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);

            showValidateFunctions = GUILayout.Toggle(showValidateFunctions, "Show Validate",
                EditorStyles.toolbarButton, GUILayout.Width(100));

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Total: {menuItems?.Count ?? 0} items", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();

            if (menuItems == null || menuItems.Count == 0)
            {
                EditorGUILayout.HelpBox("No menu items found. Click Refresh to scan.", MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Display categorized items
            foreach (var category in categorizedItems.OrderBy(kvp => kvp.Key))
            {
                var items = category.Value;

                // Filter items
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    items = items.Where(item =>
                        item.menuPath.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.className.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.methodName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0
                    ).ToList();

                    if (items.Count == 0)
                        continue;
                }

                if (!showValidateFunctions)
                {
                    items = items.Where(item => !item.isValidateFunction).ToList();
                    if (items.Count == 0)
                        continue;
                }

                // Category header
                categoryFoldouts[category.Key] = EditorGUILayout.Foldout(
                    categoryFoldouts[category.Key],
                    $"{category.Key} ({items.Count})",
                    true
                );

                if (categoryFoldouts[category.Key])
                {
                    EditorGUI.indentLevel++;

                    foreach (var item in items.OrderBy(i => i.priority).ThenBy(i => i.menuPath))
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                        // Menu path with priority
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(item.menuPath, EditorStyles.boldLabel);
                        if (item.priority != 1000) // Default priority
                        {
                            GUILayout.Label($"Priority: {item.priority}", EditorStyles.miniLabel);
                        }
                        if (item.isValidateFunction)
                        {
                            GUILayout.Label("[Validate]", EditorStyles.miniLabel);
                        }
                        if (!string.IsNullOrEmpty(item.shortcut))
                        {
                            GUILayout.Label($"Shortcut: {item.shortcut}", EditorStyles.miniLabel);
                        }
                        EditorGUILayout.EndHorizontal();

                        // Method details
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField($"Class: {item.className}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"Method: {item.methodName}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"Assembly: {item.assemblyName}", EditorStyles.miniLabel);
                        EditorGUI.indentLevel--;

                        EditorGUILayout.EndVertical();
                    }

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>
    /// Example usage in code
    /// </summary>
    public static class MenuItemScannerExample
    {
        [MenuItem("Tools/Log All Menu Items")]
        public static void LogAllMenuItems()
        {
            var menuItems = MenuItemScanner.GetAllMenuItems();

            Debug.Log($"=== Found {menuItems.Count} Menu Items ===");

            foreach (var item in menuItems)
            {
                Debug.Log(item.ToString());
            }

            // Log by category
            var categorized = MenuItemScanner.GetMenuItemsByCategory();
            foreach (var category in categorized)
            {
                Debug.Log($"\n{category.Key} Menu ({category.Value.Count} items):");
                foreach (var item in category.Value)
                {
                    Debug.Log($"  - {item.menuPath}");
                }
            }
        }

        [MenuItem("Tools/Export Menu Items to JSON")]
        public static void ExportMenuItemsToJSON()
        {
            var menuItems = MenuItemScanner.GetAllMenuItems();
            var json = JsonUtility.ToJson(new { items = menuItems }, true);

            var path = EditorUtility.SaveFilePanel("Save Menu Items", "", "menu_items.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"Exported {menuItems.Count} menu items to {path}");
            }
        }
    }
}
