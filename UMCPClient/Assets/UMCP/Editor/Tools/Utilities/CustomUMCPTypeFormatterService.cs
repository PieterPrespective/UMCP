
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using UnityEditor;

namespace UMCP.Editor
{
    [InitializeOnLoad]
    public static class CustomUMCPTypeFormatterService 
    {
#region << Default Formatters >>

        #endregion
        #region << Loaded Tracking >>
        /// <summary>
        /// Boolean for tracking whether the enriched converters have already been loaded
        /// </summary>
        private static bool isLoaded = false;

        /// <summary>
        /// Returns whether the enriched converters have already been loaded (after an assembly reload)
        /// </summary>
        public static bool IsLoaded => isLoaded;

        private static Stack<Action> OnLoadedActions = new Stack<Action>();


        /// <summary>
        /// Startic library with all present enriched converters
        /// </summary>
        private static List<IEnrichedJsonConverter> enrichedConverters = new List<IEnrichedJsonConverter>();

        #endregion
        #region << CustomConversion >>

        public static bool TrySerializeDirect(object _toSerialize, out string _serialResult, out Exception _serialFail, EnrichedConversionContext conversionContext)
        {
            _serialResult = "";
            _serialFail = null;
            try
            {
                _serialResult = JsonConvert.SerializeObject(_toSerialize, Formatting.None, new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                    Context = new StreamingContext(StreamingContextStates.All, conversionContext),
                    Converters = JSONConverters,
                });
                return true;
            }
            catch (Exception e)
            {
                _serialFail = e;
                return false;
            }
        }



        /// <summary>
        /// Serialize the given object as JSON as soon as all converters have been loaded, and return the result in the given callback.
        /// </summary>
        /// <param name="_toSerialize">object to serialize</param>
        /// <param name="_serializedResult">callback invoked when serialization result is ready</param>
        public static void SerializeASAP(object _toSerialize, Action<bool, string, Exception> _serializedResult)
        {
            Action serializationWrapper = () =>
            {
                try
                {
                    string result = JsonConvert.SerializeObject(_toSerialize, Formatting.None, new JsonSerializerSettings()
                    {
                        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                        Converters = JSONConverters,
                    });
                    _serializedResult(true, result, null);
                }
                catch (Exception e)
                {
                    _serializedResult(false, null, e);
                }
            };

            if(isLoaded)
                {
                serializationWrapper();
            }
            else
            {
                OnLoadedActions.Push(serializationWrapper);
            }
        }

        /// <summary>
        /// Deserialize the given JSON string to the given type as soon as all converters have been loaded, and return the result in the given callback.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="_toDeserialize">object to deserialize</param>
        /// <param name="_deserializedResult">callback invoked when the deserialization result is ready</param>
        public static void DeserializeASAP<T>(string _toDeserialize, Action<bool, T, Exception> _deserializedResult)
        {
            Action deserializationWrapper = () =>
            {
                try
                {
                    UnityEngine.Debug.Log($"Deserialiizing instance of type {typeof(T).Name} with data: {_toDeserialize}");
                    T result = JsonConvert.DeserializeObject<T>(_toDeserialize, new JsonSerializerSettings()
                    {
                        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                        Converters = JSONConverters,
                    });
                    _deserializedResult(true, result, null);
                }
                catch (Exception e)
                {
                    _deserializedResult(false, default, e);
                }
            };
            if (isLoaded)
            {
                deserializationWrapper();
            }
            else
            {
                OnLoadedActions.Push(deserializationWrapper);
            }
        }

        public static bool TryPopulateUnityObjectDirect<T>(ref T _existingInstance, string _serialData, out Exception _populationError) where T : UnityEngine.Object
        {
            _populationError = null;
            if (!TryFindBestConverter(typeof(T), out IEnrichedJsonConverter _convertor, out int _matchDistance))
            {
                _populationError = new Exception($"No converter found for type {typeof(T).Name}, cannot populate instance.");
                return false;
            }

            var serializer = JsonSerializer.Create(new JsonSerializerSettings()
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                Converters = JSONConverters,
            });

            JsonReader jsonReader = new JsonTextReader(new System.IO.StringReader(_serialData));
            try
            {
                ((Newtonsoft.Json.JsonConverter<T>)_convertor).ReadJson(jsonReader, typeof(T), _existingInstance, true, serializer);
                return true;
            }
            catch (Exception e)
            {
                _populationError = e;
                return false;
            }
        }


        /// <summary>
        /// Populate the given existing instance with data from the given JSON string as soon as all converters have been loaded, and return the result in the given callback.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="_serialData"></param>
        /// <param name="_existingInstance"></param>
        /// <param name="_populatedResult"></param>
        public static void PopulateUnityObjectASAP<T>(T _existingInstance, string _serialData, Action<bool, T, Exception> _populatedResult) where T : UnityEngine.Object
        {
            Action populationWrapper = () =>
            {
                try
                {
                    if (!TryFindBestConverter(typeof(T), out IEnrichedJsonConverter _convertor, out int _matchDistance))
                        {
                        _populatedResult(false, default, new Exception($"No converter found for type {typeof(T).Name}, cannot populate instance."));
                        }

                    var serializer = JsonSerializer.Create(new JsonSerializerSettings()
                    {
                        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                        Converters = JSONConverters,
                    });

                    JsonReader jsonReader = new JsonTextReader(new System.IO.StringReader(_serialData));

                    ((Newtonsoft.Json.JsonConverter<T>) _convertor).ReadJson(jsonReader, typeof(T), _existingInstance, true, serializer);
                    _populatedResult(true, _existingInstance, null);
                }
                catch (Exception e)
                {
                    _populatedResult(false, default, e);
                }
            };
            if (isLoaded)
            {
                populationWrapper();
            }
            else
            {
                OnLoadedActions.Push(populationWrapper);
            }
        }






        #endregion
        #region << JSON Converter access >>

        /// <summary>
        /// Lazily returns all enriched converters as JSONConverters for use in a JSON Conversion
        /// </summary>
        public static Newtonsoft.Json.JsonConverter[] JSONConverters
        {
            get
            {
                if(lazyJSONConverters != null)
                {
                    return lazyJSONConverters;
                }

                if(!isLoaded)
                {
                    //If not loaded yet, we cannot have any converters
                    UnityEngine.Debug.LogWarning("[CustomUMCPTypeFormatterService] Converters haven't loaded yet, note that some assemblies may not yet have been loaded - and some converters may still be missing.");
                }

                initJSONConverters();
                return lazyJSONConverters;
            }
        }
        private static Newtonsoft.Json.JsonConverter[] lazyJSONConverters = null;

        /// <summary>
        /// Initializes the lazy JSON converters array from the enriched converters list
        /// </summary>
        private static void initJSONConverters()
        {
            List<Newtonsoft.Json.JsonConverter> foundConverter = new List<Newtonsoft.Json.JsonConverter>();
            for (int i = 0; i < enrichedConverters.Count; i++)
            {
                if (enrichedConverters[i] is Newtonsoft.Json.JsonConverter converter)
                {
                    foundConverter.Add(converter);
                }
            }
            lazyJSONConverters = foundConverter.ToArray();
        }


        #endregion
        #region << Service Initialization >>

        static CustomUMCPTypeFormatterService()
        {
            isLoaded = false;

            //Force a reload of all converters on initialization
            EditorDelayCallService.Enqueue(() =>
            {
                initJSONConverters();
                isLoaded = true;
            });
            
        }




        #endregion
        #region << Converter Injection @ Initialization >>

        /// <summary>
        /// Forces a reload of all converters, e.g. after an assembly reload in the editor
        /// </summary>
        public static void ForceReloadConverters()
        {
            isLoaded = false;
            lazyJSONConverters = null;
        }


        /// <summary>
        /// Appends the given list of enriched converters to the internal list, if no converter for the same type already exists.
        /// </summary>
        /// <param name="_convertersToAdd">list with converters to add</param>
        /// <param name="_encounteredIssues">output list with converters </param>
        /// <returns></returns>
        public static bool TryAddEnrichedConverters(List<IEnrichedJsonConverter> _convertersToAdd, out List<string> _encounteredIssues)
        {
            _encounteredIssues = new List<string>();
            for (int i = 0; i < _convertersToAdd.Count; i++)
            {
                if(_convertersToAdd[i] == null)
                {
                    _encounteredIssues.Add($"Null converter found in input list at index {i}");
                    continue;
                }

                int existingIndex = findExactConverterMatch(_convertersToAdd[i].TargetType);
                if(existingIndex >= 0)
                {
                    _encounteredIssues.Add($"Converter for type {_convertersToAdd[i].TargetType.Name} already exists, injected by assembly '{enrichedConverters[existingIndex].OriginAssembly}'. Ignoring duplicate from assembly '{_convertersToAdd[i].OriginAssembly}'");
                    continue;
                }

                enrichedConverters.Add(_convertersToAdd[i]);
            }

            return (_encounteredIssues.Count == 0);
        }


        #endregion

        /// <summary>
        /// Returns the index of an exact converter match for the given type, or -1 if none exists.
        /// </summary>
        /// <param name="_type">the type we're looking for</param>
        /// <returns>index of the converter we were looking for</returns>
        private static int findExactConverterMatch(System.Type _type)
        {
            for (int i = 0; i < enrichedConverters.Count; i++)
            {
                if (enrichedConverters[i].TargetType == _type)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Tries to find the best matching converter for the given type, considering inheritance hierarchy.
        /// </summary>
        /// <param name="_type">type we're looking for</param>
        /// <returns>whether an option could be found</returns>
        public static bool TryFindBestConverter(System.Type _type, out IEnrichedJsonConverter _convertor, out int _matchDistance)
        {
            //Returns the converter the closest type or parent type of the given type
            //First check for exact match
            int exactMatchIndex = findExactConverterMatch(_type);
            if (exactMatchIndex >= 0)
            {
                _convertor = enrichedConverters[exactMatchIndex];
                _matchDistance = 0;
                return true;
            }

            //If no exact match, find the closest parent type
            IEnrichedJsonConverter bestMatch = null;
            int bestDistance = int.MaxValue;
            foreach (var converter in enrichedConverters)
            {
                int distance = getInheritanceDistance(_type, converter.TargetType);
                if (distance >= 0 && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = converter;
                }
            }

            if (bestMatch != null)
            {
                _convertor = bestMatch;
                _matchDistance = bestDistance;
                return true;
            }
            _convertor = null;
            _matchDistance = -1;
            return false;
        }

        /// <summary>
        /// Returns the inheritance distance between derived and baseType.
        /// </summary>
        /// <param name="derived">the derived type we're looking to compare</param>
        /// <param name="baseType">the base input type</param>
        /// <returns>inheritance distance between derived and baseType</returns>
        private static int getInheritanceDistance(Type derived, Type baseType)
        {
            if (!baseType.IsAssignableFrom(derived))
                return -1;

            // For interfaces, we can't easily determine distance
            if (baseType.IsInterface)
            {
                return derived.GetInterfaces().Contains(baseType) ? 1 : -1;
            }

            // For classes, count the inheritance chain
            int distance = 0;
            Type current = derived;

            while (current != null && current != baseType)
            {
                distance++;
                current = current.BaseType;
            }

            return current == baseType ? distance : -1;
        }
    }

    /// <summary>
    /// Interface for enriched JSON converters that provide additional metadata about the types they handle.
    /// </summary>
    public interface IEnrichedJsonConverter
    {
        /// <summary>
        /// The target type this converter handles
        /// </summary>
        public System.Type TargetType { get; }

        /// <summary>
        /// Assembly that injected this converter
        /// </summary>
        public string OriginAssembly { get; }

        /// <summary>
        /// Knowledge about the type this converter handles, e.g. "Vector3 stores a vector in 3D space with x,y,z components"
        /// </summary>
        public string TargetTypeKnowledge { get; }
    }

    public class InlineEnrichedJsonConvertor<T> : EnrichedJsonConvertor<T>
    {
        // <summary>
        /// The target type this converter handles
        /// </summary>
        public override System.Type TargetType => typeof(T);

        /// <summary>
        /// Knowledge about the type this converter handles, e.g. "Vector3 stores a vector in 3D space with x,y,z components"
        /// </summary>
        public string TypeKnowledge;
        public override string TargetTypeKnowledge => TypeKnowledge;

        /// <summary>
        /// Name of the assembly that injected this converter
        /// </summary>
        public string OriginAssemblyName;
        public override string OriginAssembly => OriginAssemblyName;

        /// <summary>
        /// Action to write the JSON representation of the object.
        /// </summary>

        public Action<JsonWriter, T, JsonSerializer> WriteAction;

        /// <summary>
        /// Action to read the JSON representation of the object.
        /// </summary>

        public Func<JsonReader, Type, T, bool, JsonSerializer, T> ReadFunc;

        public InlineEnrichedJsonConvertor(string typeKnowledge, Func<JsonReader, Type, T, bool, JsonSerializer, T> readFunc, Action<JsonWriter, T, JsonSerializer> writeAction)
            : base()
        {
            TypeKnowledge = typeKnowledge;
            ReadFunc = readFunc;
            WriteAction = writeAction;
        }

        public override T ReadJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (ReadFunc != null)
            {
                return ReadFunc(reader, objectType, existingValue, hasExistingValue, serializer);
            }
            throw new NotImplementedException($"ReadJson not implemented for type {typeof(T).Name}");
        }

        public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
        {
            if (WriteAction != null)
            {
                WriteAction(writer, value, serializer);
                return;
            }
            throw new NotImplementedException($"WriteJson not implemented for type {typeof(T).Name}");
        }
    }

    /// <summary>
    /// Provides a customizable JSON converter for serializing and deserializing objects of type <typeparamref
    /// name="T"/>.
    /// </summary>
    /// <remarks>This converter allows users to define custom serialization and deserialization logic through
    /// the <see cref="WriteAction"/> and <see cref="ReadFunc"/> delegates, respectively. It is particularly useful for
    /// scenarios where the default behavior of the Newtonsoft.Json library needs to be overridden or
    /// extended.</remarks>
    /// <typeparam name="T">The type of object to be serialized or deserialized.</typeparam>
    public abstract class EnrichedJsonConvertor<T> : Newtonsoft.Json.JsonConverter<T>, IEnrichedJsonConverter
    {
        /// <summary>
        /// The target type this converter handles
        /// </summary>
        public abstract System.Type TargetType { get; }

        public abstract string TargetTypeKnowledge {get;}
        public abstract string OriginAssembly { get; }
    }

    public class EnrichedConversionContext
    { 
        public enum GameObjectSerializationMode
        {
            TransformOnly, //Only serialize the transform (position, rotation, scale) of the GameObject - list the components only by type name
            AllComponents, //Serialize all components fully
        }

        public GameObjectSerializationMode gameObjectSerializationMode = GameObjectSerializationMode.TransformOnly;

        public bool WasModified = false;
    }


}
