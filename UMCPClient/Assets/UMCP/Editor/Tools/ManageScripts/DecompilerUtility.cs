using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using System;
using System.Configuration.Assemblies;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UMCP.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace UMCP.Editor.Tools.ManageScripts
{
    /// <summary>
    /// Utility class for decompiling classes from Dynamic Link Libraries.
    /// Uses reflection-based approach to extract type information since ICSharpCode.Decompiler
    /// is not readily available in Unity.
    /// </summary>
    public static class DecompilerUtility
    {
        /// <summary>
        /// Decompiles a class from a DLL and saves it to a file.
        /// </summary>
        /// <param name="libraryPath">Path to the DLL file</param>
        /// <param name="libraryClass">Fully qualified class name to decompile</param>
        /// <param name="outputFilePath">Full path for saving the file</param>
        /// <param name="message">output process message</param>
        /// <param name="overrideWhenExists">whether to override the file if it already exists (i.e. was extracted earlier)</param>
        /// <returns>whether decompilation was succesful</returns>
        public static bool DecompileClass(string libraryPath, string libraryClass, string outputFilePath, out string message, bool overrideWhenExists = false)
        {
            message = null;

            //CASE : no output file path provided
            if (string.IsNullOrEmpty(outputFilePath))
            {
                message = "[DecompilerUtility] Unable to Decompile, 'outputFilePath' parameter is required";
                return false;
            }

            //CASE : output file already exists and override is false
            FileInfo outputFile = new FileInfo(outputFilePath);
            if (outputFile.Exists)
            {
                if (!overrideWhenExists)
                {
                    message = $"[DecompilerUtility] Output file already exists and override is false: {outputFile.FullName}";
                    return false;
                }
                else
                {
                    outputFile.Delete();
                }

            }
            //CASE : output directory does not exist, create it
            else if (!outputFile.Directory.Exists)
            {
            outputFile.Directory.Create();
            }

            //CASE : no library path or class provided
            if (string.IsNullOrEmpty(libraryPath))
            {
                message = "[DecompilerUtility] Unable to Decompile, 'libraryPath' parameter is required";
                return false;
            }

            //CASE : no library class provided
            if (string.IsNullOrEmpty(libraryClass))
            {
                Debug.LogError("[DecompilerUtility] Unable to Decompile, 'libraryClass' parameter is required");
                return false;
            }

            try
            {
                // Get absolute path for the library
                string absoluteLibraryPath = GetAbsoluteLibraryPath(libraryPath);
                
                if (!File.Exists(absoluteLibraryPath))
                {
                    message = $"[DecompilerUtility] Unable to Decompile, Library file not found at path: {absoluteLibraryPath}";
                    return false;
                }
                
                // Load the assembly
                Assembly assembly = LoadAssembly(absoluteLibraryPath);
                if (assembly == null)
                {
                    message = $"[DecompilerUtility] Unable to Decompile, Failed to load assembly at path: {absoluteLibraryPath}, are you sure its a valid c# .net assembly?";
                    return false;
                }

                //Actually decompile using ICSharpCode.Decompiler
                string decompiledCode = decompileSpecificClass(absoluteLibraryPath, libraryClass);

                
                // Save to file
                string outputPath = SaveDecompiledCode(decompiledCode, outputFilePath, libraryClass);

                Debug.Log($"[DecompilerUtility] Decompiled code length: {decompiledCode?.Length ?? 0} characters to output path '{outputPath}' ('{outputFilePath}')");

                if (!string.IsNullOrEmpty(outputPath) && new FileInfo(outputFilePath).Exists)
                {
                    message = $"[DecompilerUtility] Successfully decompiled '{libraryClass}' to '{outputPath}'";
                    return true;
                }
                else
                {    
                    message = "[DecompilerUtility] Decompilation failed, unknown error saving file.";
                    return false;
                }


            }
            catch (Exception e)
            {
                message = $"[DecompilerUtility] Error decompiling class: {e.Message}\n{e.StackTrace}";
                return false;
            }
        }

        /// <summary>
        /// Actually decompiles a specific class from an assembly using ICSharpCode.Decompiler
        /// </summary>
        /// <param name="assemblyPath">assembly path for decompilation</param>
        /// <param name="fullClassName">full class name we want to target</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Exception resulting from faulty extraction</exception>
        private static string decompileSpecificClass(string assemblyPath, string fullClassName)
        {
            // Create decompiler settings
            var settings = new DecompilerSettings()
            {
                ThrowOnAssemblyResolveErrors = false
            };

            // Create the decompiler
            var decompiler = new CSharpDecompiler(assemblyPath, settings);

            // Find the type definition by full name
            var typeDefinition = decompiler.TypeSystem.FindType(new FullTypeName(fullClassName));

            if (typeDefinition == null)
            {
                throw new InvalidOperationException($"Type '{fullClassName}' not found in assembly.");
            }

            FullTypeName ft = new FullTypeName(typeDefinition.FullName);
            // Decompile the specific type
            var decompiledCode = decompiler.DecompileTypeAsString(ft);

            return decompiledCode;
        }

        /// <summary>
        /// Loads an assembly from a file path.
        /// </summary>
        private static Assembly LoadAssembly(string absolutePath)
        {
            try
            {
                // First try to find if already loaded
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in loadedAssemblies)
                {
                    if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    {
                        if (string.Equals(assembly.Location, absolutePath, StringComparison.OrdinalIgnoreCase))
                        {
                            return assembly;
                        }
                    }
                }
                
                // If not loaded, load it
                return Assembly.LoadFrom(absolutePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DecompilerUtility] Failed to load assembly: {e.Message}");
                return null;
            }
        }

        ///// <summary>
        ///// Finds a type in an assembly by its fully qualified name.
        ///// </summary>
        //private static Type FindTypeInAssembly(Assembly assembly, string typeName)
        //{
        //    try
        //    {
        //        // Try direct lookup first
        //        Type type = assembly.GetType(typeName);
        //        if (type != null)
        //        {
        //            return type;
        //        }
                
        //        // If not found, try searching all types
        //        var types = assembly.GetTypes();
                
        //        // Try exact match on full name
        //        type = types.FirstOrDefault(t => t.FullName == typeName);
        //        if (type != null)
        //        {
        //            return type;
        //        }
                
        //        // Try match on just the class name (without namespace)
        //        string className = typeName.Contains(".") ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;
        //        type = types.FirstOrDefault(t => t.Name == className);
        //        if (type != null)
        //        {
        //            Debug.LogWarning($"[DecompilerUtility] Found type by name only: {type.FullName}");
        //            return type;
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Debug.LogError($"[DecompilerUtility] Error finding type in assembly: {e.Message}");
        //    }
            
        //    return null;
        //}


        ///// <summary>
        ///// Appends type declaration to the string builder.
        ///// </summary>
        //private static void AppendTypeDeclaration(StringBuilder sb, Type type, string indent)
        //{
        //    // Type modifiers and declaration
        //    string typeKind = type.IsInterface ? "interface" : 
        //                     type.IsEnum ? "enum" : 
        //                     type.IsValueType ? "struct" : "class";
            
        //    string modifiers = "";
        //    if (type.IsPublic) modifiers += "public ";
        //    else if (type.IsNestedPublic) modifiers += "public ";
        //    else modifiers += "internal ";
            
        //    if (type.IsAbstract && !type.IsInterface) modifiers += "abstract ";
        //    if (type.IsSealed && !type.IsValueType) modifiers += "sealed ";
            
        //    sb.Append($"{indent}{modifiers}{typeKind} {type.Name}");
            
        //    // Generic parameters
        //    if (type.IsGenericTypeDefinition)
        //    {
        //        var genericParams = type.GetGenericArguments();
        //        if (genericParams.Length > 0)
        //        {
        //            sb.Append("<");
        //            sb.Append(string.Join(", ", genericParams.Select(p => p.Name)));
        //            sb.Append(">");
        //        }
        //    }
            
        //    // Base type and interfaces
        //    var baseType = type.BaseType;
        //    var interfaces = type.GetInterfaces();
            
        //    if ((baseType != null && baseType != typeof(object) && baseType != typeof(ValueType)) || interfaces.Length > 0)
        //    {
        //        sb.Append(" : ");
        //        var inheritance = new System.Collections.Generic.List<string>();
                
        //        if (baseType != null && baseType != typeof(object) && baseType != typeof(ValueType))
        //        {
        //            inheritance.Add(GetTypeName(baseType));
        //        }
                
        //        foreach (var iface in interfaces)
        //        {
        //            // Skip interfaces that are inherited from base type
        //            if (baseType == null || !baseType.GetInterfaces().Contains(iface))
        //            {
        //                inheritance.Add(GetTypeName(iface));
        //            }
        //        }
                
        //        sb.Append(string.Join(", ", inheritance));
        //    }
            
        //    sb.AppendLine();
        //    sb.AppendLine($"{indent}{{");
            
        //    // Enum values
        //    if (type.IsEnum)
        //    {
        //        AppendEnumValues(sb, type, indent + "    ");
        //    }
        //    else
        //    {
        //        // Fields
        //        AppendFields(sb, type, indent + "    ");
                
        //        // Properties
        //        AppendProperties(sb, type, indent + "    ");
                
        //        // Constructors
        //        AppendConstructors(sb, type, indent + "    ");
                
        //        // Methods
        //        AppendMethods(sb, type, indent + "    ");
                
        //        // Events
        //        AppendEvents(sb, type, indent + "    ");
                
        //        // Nested types
        //        AppendNestedTypes(sb, type, indent + "    ");
        //    }
            
        //    sb.AppendLine($"{indent}}}");
        //}

        ///// <summary>
        ///// Appends enum values.
        ///// </summary>
        //private static void AppendEnumValues(StringBuilder sb, Type type, string indent)
        //{
        //    var values = Enum.GetValues(type);
        //    var names = Enum.GetNames(type);
            
        //    for (int i = 0; i < names.Length; i++)
        //    {
        //        sb.Append($"{indent}{names[i]}");
                
        //        // Add value if it's not the default sequence
        //        var value = Convert.ToInt64(values.GetValue(i));
        //        if (value != i)
        //        {
        //            sb.Append($" = {value}");
        //        }
                
        //        if (i < names.Length - 1)
        //        {
        //            sb.AppendLine(",");
        //        }
        //        else
        //        {
        //            sb.AppendLine();
        //        }
        //    }
        //}

        ///// <summary>
        ///// Appends fields to the string builder.
        ///// </summary>
        //private static void AppendFields(StringBuilder sb, Type type, string indent)
        //{
        //    var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            
        //    if (fields.Length > 0)
        //    {
        //        sb.AppendLine($"{indent}// Fields");
        //        foreach (var field in fields)
        //        {
        //            if (field.IsSpecialName) continue; // Skip compiler-generated
                    
        //            string modifiers = GetMemberModifiers(field);
        //            string typeName = GetTypeName(field.FieldType);
        //            sb.AppendLine($"{indent}{modifiers}{typeName} {field.Name};");
        //        }
        //        sb.AppendLine();
        //    }
        //}

        ///// <summary>
        ///// Appends properties to the string builder.
        ///// </summary>
        //private static void AppendProperties(StringBuilder sb, Type type, string indent)
        //{
        //    var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            
        //    if (properties.Length > 0)
        //    {
        //        sb.AppendLine($"{indent}// Properties");
        //        foreach (var property in properties)
        //        {
        //            string modifiers = GetPropertyModifiers(property);
        //            string typeName = GetTypeName(property.PropertyType);
                    
        //            sb.Append($"{indent}{modifiers}{typeName} {property.Name} {{ ");
                    
        //            if (property.CanRead)
        //            {
        //                var getter = property.GetGetMethod(true);
        //                if (getter != null)
        //                {
        //                    string getterModifier = GetAccessModifier(getter);
        //                    if (getterModifier != modifiers.Trim())
        //                    {
        //                        sb.Append($"{getterModifier} ");
        //                    }
        //                    sb.Append("get; ");
        //                }
        //            }
                    
        //            if (property.CanWrite)
        //            {
        //                var setter = property.GetSetMethod(true);
        //                if (setter != null)
        //                {
        //                    string setterModifier = GetAccessModifier(setter);
        //                    if (setterModifier != modifiers.Trim())
        //                    {
        //                        sb.Append($"{setterModifier} ");
        //                    }
        //                    sb.Append("set; ");
        //                }
        //            }
                    
        //            sb.AppendLine("}");
        //        }
        //        sb.AppendLine();
        //    }
        //}

        ///// <summary>
        ///// Appends constructors to the string builder.
        ///// </summary>
        //private static void AppendConstructors(StringBuilder sb, Type type, string indent)
        //{
        //    var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            
        //    if (constructors.Length > 0)
        //    {
        //        sb.AppendLine($"{indent}// Constructors");
        //        foreach (var constructor in constructors)
        //        {
        //            string modifiers = GetAccessModifier(constructor);
        //            string parameters = GetParameterList(constructor.GetParameters());
                    
        //            sb.AppendLine($"{indent}{modifiers} {type.Name}({parameters})");
        //            sb.AppendLine($"{indent}{{");
        //            sb.AppendLine($"{indent}    // Implementation not available");
        //            sb.AppendLine($"{indent}}}");
        //        }
        //        sb.AppendLine();
        //    }
        //}

        ///// <summary>
        ///// Appends methods to the string builder.
        ///// </summary>
        //private static void AppendMethods(StringBuilder sb, Type type, string indent)
        //{
        //    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            
        //    var regularMethods = methods.Where(m => !m.IsSpecialName).ToArray();
            
        //    if (regularMethods.Length > 0)
        //    {
        //        sb.AppendLine($"{indent}// Methods");
        //        foreach (var method in regularMethods)
        //        {
        //            string modifiers = GetMethodModifiers(method);
        //            string returnType = GetTypeName(method.ReturnType);
        //            string parameters = GetParameterList(method.GetParameters());
                    
        //            // Generic parameters
        //            string genericParams = "";
        //            if (method.IsGenericMethodDefinition)
        //            {
        //                var genParams = method.GetGenericArguments();
        //                if (genParams.Length > 0)
        //                {
        //                    genericParams = "<" + string.Join(", ", genParams.Select(p => p.Name)) + ">";
        //                }
        //            }
                    
        //            sb.AppendLine($"{indent}{modifiers}{returnType} {method.Name}{genericParams}({parameters})");
        //            sb.AppendLine($"{indent}{{");
        //            sb.AppendLine($"{indent}    // Implementation not available");
        //            if (method.ReturnType != typeof(void))
        //            {
        //                sb.AppendLine($"{indent}    throw new NotImplementedException();");
        //            }
        //            sb.AppendLine($"{indent}}}");
        //        }
        //        sb.AppendLine();
        //    }
        //}

        ///// <summary>
        ///// Appends events to the string builder.
        ///// </summary>
        //private static void AppendEvents(StringBuilder sb, Type type, string indent)
        //{
        //    var events = type.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            
        //    if (events.Length > 0)
        //    {
        //        sb.AppendLine($"{indent}// Events");
        //        foreach (var evt in events)
        //        {
        //            string modifiers = GetEventModifiers(evt);
        //            string typeName = GetTypeName(evt.EventHandlerType);
        //            sb.AppendLine($"{indent}{modifiers}event {typeName} {evt.Name};");
        //        }
        //        sb.AppendLine();
        //    }
        //}

        ///// <summary>
        ///// Appends nested types to the string builder.
        ///// </summary>
        //private static void AppendNestedTypes(StringBuilder sb, Type type, string indent)
        //{
        //    var nestedTypes = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            
        //    if (nestedTypes.Length > 0)
        //    {
        //        sb.AppendLine($"{indent}// Nested Types");
        //        foreach (var nestedType in nestedTypes)
        //        {
        //            AppendTypeDeclaration(sb, nestedType, indent);
        //        }
        //    }
        //}

        ///// <summary>
        ///// Gets the modifiers for a field.
        ///// </summary>
        //private static string GetMemberModifiers(FieldInfo field)
        //{
        //    var modifiers = new System.Collections.Generic.List<string>();
            
        //    if (field.IsPublic) modifiers.Add("public");
        //    else if (field.IsFamily) modifiers.Add("protected");
        //    else if (field.IsAssembly) modifiers.Add("internal");
        //    else if (field.IsFamilyOrAssembly) modifiers.Add("protected internal");
        //    else modifiers.Add("private");
            
        //    if (field.IsStatic) modifiers.Add("static");
        //    if (field.IsInitOnly) modifiers.Add("readonly");
        //    if (field.IsLiteral) modifiers.Add("const");
            
        //    return string.Join(" ", modifiers) + " ";
        //}

        ///// <summary>
        ///// Gets the modifiers for a property.
        ///// </summary>
        //private static string GetPropertyModifiers(PropertyInfo property)
        //{
        //    var getter = property.GetGetMethod(true);
        //    var setter = property.GetSetMethod(true);
        //    var method = getter ?? setter;
            
        //    if (method == null) return "private ";
            
        //    return GetMethodModifiers(method);
        //}

        ///// <summary>
        ///// Gets the modifiers for a method.
        ///// </summary>
        //private static string GetMethodModifiers(MethodInfo method)
        //{
        //    var modifiers = new System.Collections.Generic.List<string>();
            
        //    modifiers.Add(GetAccessModifier(method));
            
        //    if (method.IsStatic) modifiers.Add("static");
        //    if (method.IsAbstract) modifiers.Add("abstract");
        //    if (method.IsVirtual && !method.IsFinal) modifiers.Add("virtual");
        //    if (method.IsFinal && method.IsVirtual) modifiers.Add("override");
            
        //    return string.Join(" ", modifiers) + " ";
        //}

        ///// <summary>
        ///// Gets the modifiers for an event.
        ///// </summary>
        //private static string GetEventModifiers(EventInfo evt)
        //{
        //    var addMethod = evt.GetAddMethod(true);
        //    if (addMethod == null) return "private ";
            
        //    return GetMethodModifiers(addMethod);
        //}

        ///// <summary>
        ///// Gets the access modifier for a method.
        ///// </summary>
        //private static string GetAccessModifier(MethodBase method)
        //{
        //    if (method.IsPublic) return "public";
        //    if (method.IsFamily) return "protected";
        //    if (method.IsAssembly) return "internal";
        //    if (method.IsFamilyOrAssembly) return "protected internal";
        //    return "private";
        //}

        ///// <summary>
        ///// Gets a readable type name.
        ///// </summary>
        //private static string GetTypeName(Type type)
        //{
        //    if (type == typeof(void)) return "void";
        //    if (type == typeof(object)) return "object";
        //    if (type == typeof(string)) return "string";
        //    if (type == typeof(bool)) return "bool";
        //    if (type == typeof(byte)) return "byte";
        //    if (type == typeof(sbyte)) return "sbyte";
        //    if (type == typeof(short)) return "short";
        //    if (type == typeof(ushort)) return "ushort";
        //    if (type == typeof(int)) return "int";
        //    if (type == typeof(uint)) return "uint";
        //    if (type == typeof(long)) return "long";
        //    if (type == typeof(ulong)) return "ulong";
        //    if (type == typeof(float)) return "float";
        //    if (type == typeof(double)) return "double";
        //    if (type == typeof(decimal)) return "decimal";
        //    if (type == typeof(char)) return "char";
            
        //    if (type.IsGenericType)
        //    {
        //        string name = type.Name.Substring(0, type.Name.IndexOf('`'));
        //        var genericArgs = type.GetGenericArguments();
        //        return $"{name}<{string.Join(", ", genericArgs.Select(GetTypeName))}>";
        //    }
            
        //    if (type.IsArray)
        //    {
        //        return GetTypeName(type.GetElementType()) + "[]";
        //    }
            
        //    return type.Name;
        //}

        ///// <summary>
        ///// Gets a parameter list string.
        ///// </summary>
        //private static string GetParameterList(ParameterInfo[] parameters)
        //{
        //    if (parameters.Length == 0) return "";
            
        //    var paramStrings = new System.Collections.Generic.List<string>();
            
        //    foreach (var param in parameters)
        //    {
        //        string paramStr = "";
                
        //        if (param.IsOut) paramStr += "out ";
        //        else if (param.ParameterType.IsByRef) paramStr += "ref ";
                
        //        paramStr += GetTypeName(param.ParameterType.IsByRef ? param.ParameterType.GetElementType() : param.ParameterType);
        //        paramStr += " " + param.Name;
                
        //        if (param.HasDefaultValue)
        //        {
        //            paramStr += " = ";
        //            if (param.DefaultValue == null)
        //            {
        //                paramStr += "null";
        //            }
        //            else if (param.DefaultValue is string)
        //            {
        //                paramStr += $"\"{param.DefaultValue}\"";
        //            }
        //            else if (param.DefaultValue is bool)
        //            {
        //                paramStr += param.DefaultValue.ToString().ToLower();
        //            }
        //            else
        //            {
        //                paramStr += param.DefaultValue.ToString();
        //            }
        //        }
                
        //        paramStrings.Add(paramStr);
        //    }
            
        //    return string.Join(", ", paramStrings);
        //}

        /// <summary>
        /// Saves the decompiled code to a file.
        /// </summary>
        private static string SaveDecompiledCode(string code, string outputFileName, string className)
        {
            try
            {
                // Get output folder from settings
                string outputFolder = UMCPSettings.Instance.DecompiledScriptsOutputFolder;
                if (string.IsNullOrEmpty(outputFolder))
                {
                    outputFolder = "UMCP/DecompiledScripts";
                }
                
                // Ensure output folder exists
                string fullOutputPath = Path.Combine(Application.dataPath, outputFolder);
                if (!Directory.Exists(fullOutputPath))
                {
                    Directory.CreateDirectory(fullOutputPath);
                }
                
                // Ensure file has .cs extension
                if (!outputFileName.EndsWith(".cs"))
                {
                    outputFileName += ".cs";
                }
                
                // Full file path
                string filePath = Path.Combine(fullOutputPath, outputFileName);
                
                // Write the file
                File.WriteAllText(filePath, code);
                
                // Return relative path for Unity
                string relativePath = "Assets/" + outputFolder + "/" + outputFileName;
                return relativePath.Replace('\\', '/');
            }
            catch (Exception e)
            {
                Debug.LogError($"[DecompilerUtility] Error saving decompiled code: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the absolute path for a library.
        /// </summary>
        private static string GetAbsoluteLibraryPath(string libraryPath)
        {
            // If already absolute, return as is
            if (Path.IsPathRooted(libraryPath))
            {
                return libraryPath;
            }
            
            // Try relative to project
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.Combine(projectPath, libraryPath).Replace('\\', '/');
            
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
            
            // Try in Library folder
            fullPath = Path.Combine(projectPath, "Library", libraryPath).Replace('\\', '/');
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
            
            // Try in PackageCache
            fullPath = Path.Combine(projectPath, "Library", "PackageCache", libraryPath).Replace('\\', '/');
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
            
            // Try in ScriptAssemblies
            fullPath = Path.Combine(projectPath, "Library", "ScriptAssemblies", libraryPath).Replace('\\', '/');
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
            
            // Return original if not found
            return libraryPath;
        }
    }
}