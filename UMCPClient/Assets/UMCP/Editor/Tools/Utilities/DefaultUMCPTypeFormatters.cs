using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UMCP.Editor
{
    [InitializeOnLoad]
    public static class DefaultUMCPTypeFormatters 
    {
        static DefaultUMCPTypeFormatters()
        {
            if (!CustomUMCPTypeFormatterService.TryAddEnrichedConverters(new List<IEnrichedJsonConverter>()
                {
                new Vector2Formatter(),
                new Vector3Formatter(),
                new QuaternionFormatter(),
                new GameObjectFormatter(),
                new TransformFormatter(),
                new SphereColliderFormatter(),
                new ColorFormatter(),
                new MaterialFormatter(),
                new ScriptableObjectFormatter(),
                new TimeLineFormatter(),
                new TimeLineEditorSettingsFormatter(),
                }, out List<string> _encounteredIssues)) { 

                for(int i = 0; i < _encounteredIssues.Count; i++) { 
                    Debug.LogWarning($"Failed to register default UMCP Type Formatter: {_encounteredIssues[i]}");
                }
            }
            
        }
    }

    public class ScriptableObjectFormatter : EnrichedJsonConvertor<ScriptableObject>
    {
        public override Type TargetType => typeof(ScriptableObject);
        public override string TargetTypeKnowledge => "Unity3D Asset that allows to store data in a custom class, useful for configuration data";
        public override string OriginAssembly => "UMCP.Editor";
        public override ScriptableObject ReadJson(JsonReader reader, Type objectType, ScriptableObject existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            Debug.Log("[ScriptableObjectFormatter] Populate operation started...");

            if (existingValue == null)
            {
                throw new ArgumentNullException("[ScriptableObjectFormatter] requires an existing ScriptableObject instance to populate");
            }

            Debug.Log("1");

            JObject jobj = JObject.Load(reader);

            if (jobj.TryGetValue("__scriptableObjectType__", out JToken? nameToken))
            {
                if (existingValue.GetType().FullName != nameToken.ToString())
                {
                    Debug.LogWarning($"[ScriptableObjectFormatter] Existing ScriptableObject type '{existingValue.GetType().FullName}' does not match serialized type '{nameToken.ToString()}'");
                    return existingValue;
                }
            }

            Debug.Log("2");

            Type scriptableOjbectType = existingValue.GetType();
            FieldInfo[] soFields = scriptableOjbectType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
            for (int i = 0; i < soFields.Length; i++)
            {
                Debug.Log("found field: " + soFields[i].Name + ", type: " + soFields[i].FieldType.FullName + " : " + jobj.ToString());

                if (jobj.TryGetValue(soFields[i].Name, out JToken? fieldToken))
                {
                    //TODO : make safe
                    soFields[i].SetValue(existingValue,
                        serializer.Deserialize(fieldToken.CreateReader(), soFields[i].FieldType));
                }
            }

            return existingValue;
        }
        public override void WriteJson(JsonWriter writer, ScriptableObject value, JsonSerializer serializer)
        {
            Type scriptableOjbectType = value.GetType();
            Debug.Log("ScriptableObjectFormatter: Serializing ScriptableObject of type " + scriptableOjbectType.FullName);
            writer.WriteStartObject();

            writer.WritePropertyName("__scriptableObjectType__");
            serializer.Serialize(writer, value.GetType().FullName);

            FieldInfo[] soFields = scriptableOjbectType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
            for(int i = 0; i < soFields.Length; i++)
            {
                writer.WritePropertyName(soFields[i].Name);
                serializer.Serialize(writer, soFields[i].GetValue(value));
            }
            writer.WriteEndObject();
        }
    }









    public class GameObjectFormatter : EnrichedJsonConvertor<GameObject>
    {
        public override Type TargetType => typeof(GameObject);
        public override string TargetTypeKnowledge => "Unity3D Object that represents objects in 3D space. It is a container for Components";
        public override string OriginAssembly => "UMCP.Editor";
        public override GameObject ReadJson(JsonReader reader, Type objectType, GameObject existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
            {
                throw new ArgumentNullException("[GameObjectFormatter] requires an existing GameObject instance to populate, did you try to run DeserializeASAP instead of PopulateASAP on a GameObject?");
            }
            JObject jobj = JObject.Load(reader);
            //Try to set name
            if (jobj.TryGetValue(nameof(GameObject.name), out JToken? nameToken))
            {
                existingValue.name = nameToken.ToString();
            }
            //Try to set active state
            if (jobj.TryGetValue(nameof(GameObject.activeSelf), out JToken? activeToken))
            {
                existingValue.SetActive(activeToken.Value<bool>());
            }
            return existingValue;
        }
        public override void WriteJson(JsonWriter writer, GameObject value, JsonSerializer serializer)
        {
            EnrichedConversionContext.GameObjectSerializationMode serialMode = EnrichedConversionContext.GameObjectSerializationMode.TransformOnly;
            EnrichedConversionContext castContextShared = null;
            if (serializer.Context.Context != null && serializer.Context.Context is EnrichedConversionContext castContext)
            {
                serialMode = castContext.gameObjectSerializationMode;
                castContextShared = castContext;
            }


            // Gather component type names regardless of serialization mode
            List<string> componentTypeNames = new List<string>();
            Component[] components = value.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    if (components[i] is Transform) continue; // Skip Transform component as it's serialized separately
                    componentTypeNames.Add(components[i].GetType().FullName);
                }
                else
                {
                    componentTypeNames.Add("null");
                }
            }

            // Gather component data only if serializing all components 
            List<object> componentData = new List<object>();
            if (serialMode == EnrichedConversionContext.GameObjectSerializationMode.AllComponents) 
            {
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null)
                    {

                        if (components[i] is Transform) continue; // Skip Transform component as it's serialized separately


                        componentData.Add(UnityObjectUtility.GetComponentData(components[i], castContextShared));
                    }
                    else
                    {
                        componentData.Add("null");
                    }
                }
            }

            //TODO : consider a mode where we only serialize specific components (this is quite context heavy);

            //Get (instanceID, componentData) where componentData (componentData.typeName=UnityEngine.BoxCollider) ??
            //"{$instanceID, $componentData[='UnityEngine.BoxCollider'{$instanceID}] }"


            writer.WriteStartObject();

            writer.WritePropertyName(nameof(GameObject.name));
            serializer.Serialize(writer, value.name);

            writer.WritePropertyName("instanceID");
            serializer.Serialize(writer, value.GetInstanceID());

            writer.WritePropertyName("parentInstanceID");
            serializer.Serialize(writer, (value.transform.parent != null) ? value.transform.parent.gameObject.GetInstanceID() : 0);

            writer.WritePropertyName(nameof(GameObject.tag));
            serializer.Serialize(writer, value.tag);

            writer.WritePropertyName(nameof(GameObject.layer));
            serializer.Serialize(writer, value.layer);

            writer.WritePropertyName(nameof(GameObject.activeSelf));
            serializer.Serialize(writer, value.activeSelf);

            writer.WritePropertyName(nameof(GameObject.activeInHierarchy));
            serializer.Serialize(writer, value.activeInHierarchy);

            writer.WritePropertyName(nameof(GameObject.isStatic));
            serializer.Serialize(writer, value.isStatic);

            writer.WritePropertyName("scenePath");
            serializer.Serialize(writer, (value.scene != null) ? value.scene.path ?? "" : "");

            writer.WritePropertyName(nameof(GameObject.transform));
            serializer.Serialize(writer, value.transform);

            writer.WritePropertyName("componentNames");
            serializer.Serialize(writer, componentTypeNames);

            if (serialMode == EnrichedConversionContext.GameObjectSerializationMode.AllComponents)
            {
                writer.WritePropertyName("componentData");
                serializer.Serialize(writer, componentData);
            }

            writer.WriteEndObject();
        }
    }









    public class TimeLineFormatter : EnrichedJsonConvertor<TimelineAsset>
    {
        public override Type TargetType => typeof(TimelineAsset);

        public override string TargetTypeKnowledge => "Asset containing a timeline based description of an animation";

        public override string OriginAssembly => "UMCP.Editor";

        public override TimelineAsset ReadJson(JsonReader reader, Type objectType, TimelineAsset existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, TimelineAsset value, JsonSerializer serializer)
        {
            AnimationClip someClip;


            IEnumerable<TrackAsset> rootTracks = value.GetRootTracks();
            int trackIndex = 0;
            foreach(TrackAsset t in rootTracks)
            {
                //if(t is)

                if(t is AnimationTrack animTrack)
                {
                    Debug.Log($"anim track: {animTrack.start} - {animTrack.end}, {animTrack.GetClips().Count()}");

                    GameObject gameObject = new GameObject();
                    animTrack.infiniteClip.SampleAnimation(gameObject,0f);
                    Debug.Log("Anim Pos: " + gameObject.transform.position + ", " + animTrack.infiniteClipOffsetPosition);

                    animTrack.infiniteClip.SampleAnimation(gameObject, 0.1f);
                    Debug.Log("Anim Pos: " + gameObject.transform.position + ", " + animTrack.infiniteClipOffsetPosition);
                    animTrack.infiniteClip.SampleAnimation(gameObject, 0.2f);
                    Debug.Log("Anim Pos: " + gameObject.transform.position);

                }

                if(t.hasCurves)
                {
                    AnimationClip clip = t.curves;
                    Debug.Log("Got Clip!" + clip.length);
                    for(int i = 0; i < clip.events.Length; i++)
                    {
                        Debug.Log("Clip event: " +  clip.events[i].functionName);
                    }
                }
                else
                    {
                    Debug.Log($"root track {trackIndex} of type {t.GetType().FullName} has no curves");
                    }

                if (t is SignalTrack signalTrack)
                    {
                        IMarker[] markers = signalTrack.GetMarkers().ToArray();
                        for (int i = 0; i < markers.Length; i++)
                        {
                            if (markers[i] is SignalEmitter emitter)
                            {
                                Debug.Log("Marker [" + i + "] = " + emitter.time + " = " + emitter.name);
                            }
                        }
                    }
                trackIndex++;
            }

            writer.WriteStartObject();
            writer.WritePropertyName(nameof(TimelineAsset.editorSettings));
            serializer.Serialize(writer, value.editorSettings);
            writer.WritePropertyName(nameof(TimelineAsset.duration));
            serializer.Serialize(writer, value.duration);
            writer.WritePropertyName(nameof(TimelineAsset.fixedDuration));
            serializer.Serialize(writer, value.fixedDuration);
            writer.WritePropertyName(nameof(TimelineAsset.durationMode));
            serializer.Serialize(writer, value.durationMode);
            writer.WritePropertyName(nameof(TimelineAsset.outputTrackCount));
            serializer.Serialize(writer, value.outputTrackCount);
            writer.WritePropertyName(nameof(TimelineAsset.rootTrackCount));
            serializer.Serialize(writer, value.rootTrackCount);
            //writer.WritePropertyName("rootTracks");
            //serializer.Serialize(writer, value.GetRootTracks());



            //writer.WritePropertyName(nameof(Material.color));
            //serializer.Serialize(writer, value.color);
            //writer.WritePropertyName(nameof(Material.mainTexture));
            //serializer.Serialize(writer, (value.mainTexture != null) ? AssetDatabase.GetAssetPath(value.mainTexture) : "");
            writer.WriteEndObject();
        }
    }

    public class TimeLineEditorSettingsFormatter : EnrichedJsonConvertor<TimelineAsset.EditorSettings>
    {
        public override Type TargetType => typeof (TimelineAsset.EditorSettings);

        public override string TargetTypeKnowledge => "Settings used by timeline for editing purposes";

        public override string OriginAssembly => "UMCP.Editor";

        public override TimelineAsset.EditorSettings ReadJson(JsonReader reader, Type objectType, TimelineAsset.EditorSettings existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
            {
                throw new ArgumentNullException("[EditorTimeLineFormatter] requires an existing TimelineAsset.EditorSettings instance to populate");
            }
            JObject jobj = JObject.Load(reader);

            //Try to set framerate
            if (jobj.TryGetValue(nameof(TimelineAsset.EditorSettings.frameRate), out JToken? frameRateToken))
            {
                existingValue.frameRate = frameRateToken.Value<double>();
            }

            //Try deserialize color
            if (jobj.TryGetValue(nameof(TimelineAsset.EditorSettings.scenePreview), out JToken? previewToken))
            {
                existingValue.scenePreview = previewToken.Value<bool>();
            }

            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, TimelineAsset.EditorSettings value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(TimelineAsset.EditorSettings.frameRate));
            serializer.Serialize(writer, value.frameRate);
            writer.WritePropertyName(nameof(TimelineAsset.EditorSettings.scenePreview));
            serializer.Serialize(writer, value.scenePreview);
            writer.WriteEndObject();
        }
    }


    //public class AnimationTrackFormatter : EnrichedJsonConvertor<AnimationTrack>
    //{
    //    public override Type TargetType => throw new NotImplementedException();

    //    public override string TargetTypeKnowledge => throw new NotImplementedException();

    //    public override string OriginAssembly => throw new NotImplementedException();

    //    public override AnimationClip ReadJson(JsonReader reader, Type objectType, AnimationTrack existingValue, bool hasExistingValue, JsonSerializer serializer)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public override void WriteJson(JsonWriter writer, AnimationTrack value, JsonSerializer serializer)
    //    {
    //        writer.WriteStartObject();

    //        //Specifies whether to apply the AvatarMask to the track.
    //        //writer.WritePropertyName(nameof(AnimationTrack.applyAvatarMask));

    //        //Specifies the AvatarMask to be applied to all clips on the track.
    //        //writer.WritePropertyName(nameof(AnimationTrack.avatarMask));

    //        //The euler angle representation of the rotation offset of the entire track.
    //        writer.WritePropertyName(nameof(AnimationTrack.eulerAngles));
    //        serializer.Serialize(writer, value.eulerAngles);

    //        //Specifies whether the Animation Track has clips, or is in infinite mode.
    //        writer.WritePropertyName(nameof(AnimationTrack.inClipMode));
    //        serializer.Serialize(writer, value.inClipMode);

    //        //An AnimationClip storing the data for an infinite track.
    //        writer.WritePropertyName(nameof(AnimationTrack.infiniteClip));
    //        serializer.Serialize(writer, value.infiniteClip);

    //        //The euler angle representation of the rotation offset of the track when in infinite mode.
    //        writer.WritePropertyName(nameof(AnimationTrack.infiniteClipOffsetEulerAngles));
    //        serializer.Serialize(writer, value.infiniteClipOffsetEulerAngles);

    //        //The translation offset of a track in infinite mode.
    //        writer.WritePropertyName(nameof(AnimationTrack.infiniteClipOffsetPosition));
    //        serializer.Serialize(writer, value.infiniteClipOffsetPosition);

    //        //The rotation offset of a track in infinite mode.
    //        writer.WritePropertyName(nameof(AnimationTrack.infiniteClipOffsetRotation));
    //        serializer.Serialize(writer, value.infiniteClipOffsetRotation);

    //        //The saved state of post-extrapolation for clips when converted to infinite mode.
    //        writer.WritePropertyName(nameof(AnimationTrack.infiniteClipPostExtrapolation));
    //        serializer.Serialize(writer, value.infiniteClipPostExtrapolation);

    //        //The saved state of pre-extrapolation for clips converted to infinite mode.
    //        writer.WritePropertyName(nameof(AnimationTrack.infiniteClipPreExtrapolation));
    //        serializer.Serialize(writer, value.infiniteClipPreExtrapolation);

    //        //Specifies which fields to match when aligning offsets of clips.
    //        writer.WritePropertyName(nameof(AnimationTrack.matchTargetFields));
    //        serializer.Serialize(writer, value.matchTargetFields);

    //        //The translation offset of the entire track.
    //        writer.WritePropertyName(nameof(AnimationTrack.position));
    //        serializer.Serialize(writer, value.position);

    //        //The rotation offset of the entire track, expressed as a quaternion.
    //        writer.WritePropertyName(nameof(AnimationTrack.rotation));
    //        serializer.Serialize(writer, value.rotation);

    //        //Specifies what is used to set the starting position and orientation of an Animation Track.
    //        writer.WritePropertyName(nameof(AnimationTrack.trackOffset));
    //        serializer.Serialize(writer, value.trackOffset);


    //        writer.WritePropertyName(nameof(TimelineAsset.EditorSettings.scenePreview));
    //        serializer.Serialize(writer, value.scenePreview);
    //        writer.WriteEndObject();




    //        throw new NotImplementedException();
    //    }
    //}








    public class MaterialFormatter : EnrichedJsonConvertor<Material>
    {
        public override Type TargetType => typeof(Material);
        public override string TargetTypeKnowledge => "Unity3D Asset that defines the visual appearance of a 3D object, including its shader and texture properties";
        public override string OriginAssembly => "UMCP.Editor";
        public override Material ReadJson(JsonReader reader, Type objectType, Material existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
            {
                throw new ArgumentNullException("[MaterialFormatter] requires an existing Material instance to populate, did you try to run DeserializeASAP instead of PopulateASAP on an Asset?");
            }

            JObject jobj = JObject.Load(reader);

            //Try to load shader
            if (jobj.TryGetValue(nameof(Material.shader), out JToken? shaderToken))
            {
                existingValue.shader = Shader.Find(shaderToken.ToString()) ?? Shader.Find("Standard");
            }

            //Try deserialize color
            if (jobj.TryGetValue(nameof(Material.color), out JToken? colorToken))
            {
                existingValue.color = JSONUtility.SafeDeserialize<Color>(serializer, colorToken);
            }

            //Try set texture via path (currently we don't support memory textures)
            if(jobj.TryGetValue(nameof(Material.mainTexture), out JToken? mainTexToken))
            {
                string texPath = mainTexToken.ToString();
                if (string.IsNullOrEmpty(texPath))
                {
                    existingValue.mainTexture = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                }
                else
                {
                    existingValue.mainTexture = null;
                }
            }
            return existingValue;
        }
        public override void WriteJson(JsonWriter writer, Material value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(Material.shader));
            serializer.Serialize(writer, value.shader.name);
            writer.WritePropertyName(nameof(Material.color));
            serializer.Serialize(writer, value.color);
            writer.WritePropertyName(nameof(Material.mainTexture));
            serializer.Serialize(writer, (value.mainTexture != null) ? AssetDatabase.GetAssetPath(value.mainTexture) : "");
            writer.WriteEndObject();
        }
    }








    public class SphereColliderFormatter : EnrichedJsonConvertor<SphereCollider>
    {
        public override Type TargetType => typeof(SphereCollider);
        public override string TargetTypeKnowledge => "Unity3D Component that adds a spherical collider to a GameObject";
        public override string OriginAssembly => "UMCP.Editor";
        public override SphereCollider ReadJson(JsonReader reader, Type objectType, SphereCollider existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            //UnityEngine.Debug.Log("READ: " + reader.ToString() + ", path:" + reader.Path.ToString());
            if (existingValue == null)
            {
                throw new ArgumentNullException("[SphereColliderFormatter] requires an existing SphereCollider instance to populate, did you try to run DeserializeASAP instead of PopulateASAP on a Component?");
            }
            JObject jobj = JObject.Load(reader);
            if (jobj.TryGetValue(nameof(SphereCollider.center), out JToken? centerToken))
                {
                UnityEngine.Debug.Log(JSONUtility.SafeDeserialize<Vector3>(serializer, centerToken));
                existingValue.center = JSONUtility.SafeDeserialize<Vector3>(serializer, centerToken);
                }
            if(jobj.TryGetValue(nameof(SphereCollider.radius), out JToken? radiusToken))
                {
                existingValue.radius = JSONUtility.SafeDeserialize<float>(serializer, radiusToken, 0f);
                }
            if (jobj.TryGetValue(nameof(SphereCollider.isTrigger), out JToken? isTriggerToken))
                {
                existingValue.isTrigger = JSONUtility.SafeDeserialize<bool>(serializer, isTriggerToken, false);
                }
            return existingValue;
        }
        public override void WriteJson(JsonWriter writer, SphereCollider value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(SphereCollider.center));
            serializer.Serialize(writer, value.center);
            writer.WritePropertyName(nameof(SphereCollider.radius));
            serializer.Serialize(writer, value.radius);
            writer.WritePropertyName(nameof(SphereCollider.isTrigger));
            serializer.Serialize(writer, value.isTrigger);
            writer.WriteEndObject();
        }
    }











    public class TransformFormatter : EnrichedJsonConvertor<Transform>
    {
        public override Type TargetType => typeof(Transform);
        public override string TargetTypeKnowledge => "Unity3D Component that manages a gameobjects' Transform - it contains the position, rotation and scale of an object in 3D space with 32-bit precision";
        public override string OriginAssembly => "UMCP.Editor";
        public override Transform ReadJson(JsonReader reader, Type objectType, Transform existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            UnityEngine.Debug.Log("READ: " + reader.ToString() + ", path:" + reader.Path.ToString());
            if (existingValue == null)
            {
                throw new ArgumentNullException("[TransformFormatter] requires an existing Transform instance to populate, did you try to run DeserializeASAP instead of PopulateASAP on a Component?");
            }

            JObject jobj = JObject.Load(reader);
            if (jobj.TryGetValue(nameof(Transform.localPosition), out JToken? positionToken))
                {
                UnityEngine.Debug.Log(JSONUtility.SafeDeserialize<Vector3>(serializer, positionToken));

                existingValue.localPosition = JSONUtility.SafeDeserialize<Vector3>(serializer, positionToken);
                }

            if(jobj.TryGetValue(nameof(Transform.localRotation), out JToken? rotationToken))
                {
                existingValue.localRotation = JSONUtility.SafeDeserialize<Quaternion>(serializer, rotationToken, Quaternion.identity);
                }
            else if(jobj.TryGetValue(nameof(Transform.localEulerAngles), out JToken? eulerToken))
                {
                // Fallback to euler angles if quaternion is not available
                existingValue.localRotation = Quaternion.Euler(JSONUtility.SafeDeserialize<Vector3>(serializer, eulerToken, Vector3.zero));
                }

            if (jobj.TryGetValue(nameof(Transform.localScale), out JToken? scaleToken))
                {
                existingValue.localScale = JSONUtility.SafeDeserialize<Vector3>(serializer, scaleToken, Vector3.one);
                }

            return existingValue;
        }
        public override void WriteJson(JsonWriter writer, Transform value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(Transform.localPosition));
            serializer.Serialize(writer, value.localPosition);

            writer.WritePropertyName(nameof(Transform.localRotation));
            serializer.Serialize(writer, value.localRotation);

            writer.WritePropertyName(nameof(Transform.localEulerAngles));
            serializer.Serialize(writer, value.localEulerAngles);

            writer.WritePropertyName(nameof(Transform.localScale));
            serializer.Serialize(writer, value.localScale);

            writer.WritePropertyName(nameof(Transform.position));
            serializer.Serialize(writer, value.position);

            writer.WritePropertyName(nameof(Transform.rotation));
            serializer.Serialize(writer, value.rotation);

            writer.WritePropertyName(nameof(Transform.eulerAngles));
            serializer.Serialize(writer, value.eulerAngles);

            writer.WritePropertyName(nameof(Transform.forward));
            serializer.Serialize(writer, value.forward);

            writer.WritePropertyName(nameof(Transform.right));
            serializer.Serialize(writer, value.right);

            writer.WritePropertyName(nameof(Transform.up));
            serializer.Serialize(writer, value.up);
            
            writer.WriteEndObject();
        }
    }


    public class ColorFormatter : EnrichedJsonConvertor<Color>
    {
        public override Type TargetType => typeof(Color);
        public override string TargetTypeKnowledge => "Color stores RGBA color with 32-bit float r,g,b,a components";
        public override string OriginAssembly =>
            "UMCP.Editor";

        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = serializer.Deserialize(reader);
            var iv = JsonConvert.DeserializeObject<Color>(t.ToString());
            return iv;
        }

        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(Color.r));
            writer.WriteValue(value.r);
            writer.WritePropertyName(nameof(Color.g));
            writer.WriteValue(value.g);
            writer.WritePropertyName(nameof(Color.b));
            writer.WriteValue(value.b);
            writer.WritePropertyName(nameof(Color.a));
            writer.WriteValue(value.a);
            writer.WriteEndObject();
        }
    }


        public class QuaternionFormatter : EnrichedJsonConvertor<Quaternion>
    {
        public override Type TargetType => typeof(Quaternion);
        public override string TargetTypeKnowledge => "Quaternion stores a rotation in 3D space with 32-bit float x,y,z,w components";
        public override string OriginAssembly => "UMCP.Editor";

        public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = serializer.Deserialize(reader);
            var iv = JsonConvert.DeserializeObject<Quaternion>(t.ToString());
            return iv;
        }

        public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(Quaternion.x));
            writer.WriteValue(value.x);
            writer.WritePropertyName(nameof(Quaternion.y));
            writer.WriteValue(value.y);
            writer.WritePropertyName(nameof(Quaternion.z));
            writer.WriteValue(value.z);
            writer.WritePropertyName(nameof(Quaternion.w));
            writer.WriteValue(value.w);
            writer.WriteEndObject();
        }
    }

    public class Vector2Formatter : EnrichedJsonConvertor<Vector2>
        {
            public override Type TargetType => typeof(Vector2);
            public override string TargetTypeKnowledge => "Vector2 stores a vector in 2D space with 32-bit float x,y components";
            public override string OriginAssembly => "UMCP.Editor";

            public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var t = serializer.Deserialize(reader);
                var iv = JsonConvert.DeserializeObject<Vector2>(t.ToString());
                return iv;
            }

            public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(nameof(Vector3.x));
                writer.WriteValue(value.x);
                writer.WritePropertyName(nameof(Vector3.y));
                writer.WriteValue(value.y);
                writer.WriteEndObject();
            }
        };

        public class Vector3Formatter : EnrichedJsonConvertor<Vector3>
        {
            public override Type TargetType => typeof(Vector3);

            public override string TargetTypeKnowledge => "Vector3 stores a vector in 3D space with 32-bit float x,y,z components";

            public override string OriginAssembly => "UMCP.Editor";

            public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                UnityEngine.Debug.Log("VECTOR3 READ: " + reader.ToString() + ", path:" + reader.Path.ToString());

                var t = serializer.Deserialize(reader);
                var iv = JsonConvert.DeserializeObject<Vector3>(t.ToString());
                return iv;
            }

            public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(nameof(Vector3.x));
                writer.WriteValue(value.x);
                writer.WritePropertyName(nameof(Vector3.y));
                writer.WriteValue(value.y);
                writer.WritePropertyName(nameof(Vector3.z));
                writer.WriteValue(value.z);
                writer.WriteEndObject();
            }
        }


    /// <summary>
    /// Utility class for JSON serialization operations
    /// </summary>
    public static class JSONUtility
    {
        /// <summary>
        /// Checks if a JToken is null or empty
        /// </summary>
        /// <param name="_obj">The JToken to check</param>
        /// <returns>True if the token is null or empty</returns>
        public static bool IsNull(JToken _obj)
        {
            return _obj == null || string.IsNullOrEmpty(_obj.ToString()) || _obj.ToString() == "null";
        }


        /// <summary>
        /// Safely deserializes a JToken to the specified type
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="serializer">The JSON serializer to use</param>
        /// <param name="_obj">The JToken to deserialize</param>
        /// <returns>The deserialized object or null if deserialization fails</returns>
        public static T SafeDeserialize<T>(JsonSerializer serializer, JToken _obj, T defaultOverride = default) 
        {
            if (IsNull(_obj))
            {
                return defaultOverride; // return null for reference types, default value for value types
            }

            try
            {
                return serializer.Deserialize<T>(_obj.CreateReader());
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to deserialize object of type {typeof(T)} with error: {e.Message}, Value: '" + _obj.ToString() + "'");
                throw e;
            }
        }
    }
}