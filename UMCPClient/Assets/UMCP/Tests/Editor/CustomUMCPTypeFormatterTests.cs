using NUnit.Framework;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using UMCP.Editor;
using UMCP.Editor.Helpers;
using UMCP.Editor.Serialization;
using UMCP.Editor.Tools;
using UnityEngine;
using UnityEngine.TestTools;
using static UnityEngine.GraphicsBuffer;

namespace UMCP.Tests.Editor
{
    public class CustomUMCPTypeFormatterTests 
    {
        [UnityTest]
        public IEnumerator TestSphereColliderConversion()
        {
            GameObject Sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            Component[] components = Sphere.GetComponents<Component>();
            foreach (Component comp in components)
            {
                UnityEngine.Debug.Log($"Component: {comp.GetType().Name}");
            }

            var componentData = components.Select(c => ManageGameObject.GetComponentData(c)).ToList();

            string responseJson = JSONConversionUtility.SerializeObject(componentData);

            UnityEngine.Debug.Log($"Serialized Sphere Collider Data: {responseJson}");

            yield return null; // Wait a frame
        }





        [UnityTest]
        public IEnumerator TestTransformJSONConversion()
        {
            GameObject gameObject = new GameObject("TestObject");
            Transform originalTransform = gameObject.transform;
            originalTransform.position = new Vector3(1, 2, 3);
            originalTransform.rotation = Quaternion.Euler(45, 90, 0);
            originalTransform.localScale = new Vector3(1, 2, 1);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            bool serializationCompleted = false;
            string serializedData = null;

            CustomUMCPTypeFormatterService.SerializeASAP(originalTransform, (_success, _serial, _exception) => {
                serializedData = _serial;
                serializationCompleted = true;
            });

            yield return new WaitUntil(() => serializationCompleted || sw.ElapsedMilliseconds > 5000);

            if(sw.ElapsedMilliseconds > 5000) { 
                UnityEngine.Debug.LogError("Serialization took too long");
                yield break;
            }

            UnityEngine.Debug.Log($"Serialized Transform: {serializedData}");

            GameObject gameObject2 = new GameObject("TestObject2");
            Transform deserializedTransform = gameObject2.transform;


            UnityEngine.Debug.Log("Starting partial data test...");
            // Test with partial data (missing rotation and scale)
            string partialSerializedData = "{\"localPosition\":{\"x\":1.0,\"y\":2.0,\"z\":3.0}}"; // Test with only localPosition

            serializationCompleted = false;
            sw.Restart();
            CustomUMCPTypeFormatterService.PopulateUnityObjectASAP<Transform>(deserializedTransform, partialSerializedData, (_success, _obj, _exception) => {


                if (_exception != null)
                {
                    UnityEngine.Debug.LogError(_exception);
                    return;
                }

                if (_obj == null)
                {
                    UnityEngine.Debug.LogError("Deserialized object is null");
                    return;
                }


                UnityEngine.Debug.Log(_obj.localPosition);


                serializationCompleted = true;
            });

            UnityEngine.Debug.Log("Completing partial data test...");

            yield return new WaitUntil(() => serializationCompleted || sw.ElapsedMilliseconds > 5000);

            Assert.AreEqual(0f, Vector3.Distance(originalTransform.position, deserializedTransform.position), 1e-5f, "Position did not match after serialization/deserialization");
            Assert.AreEqual(0f, Quaternion.Angle(deserializedTransform.rotation, Quaternion.identity), 1e-5f, "Rotation did not match after serialization/deserialization");
            Assert.AreEqual(0f, Vector3.Distance(deserializedTransform.localScale, Vector3.one), 1e-5f, "Scale did not match after serialization/deserialization");



            serializationCompleted = false;
            sw.Restart();
            CustomUMCPTypeFormatterService.PopulateUnityObjectASAP<Transform>(deserializedTransform, serializedData, (_success, _obj, _exception) => {

                if(_exception != null) { 
                    UnityEngine.Debug.LogError(_exception);
                    return;
                }

                if(_obj == null) { 
                    UnityEngine.Debug.LogError("Deserialized object is null");
                    return;
                }


                UnityEngine.Debug.Log(_obj.localPosition);


                serializationCompleted = true;
            });

            yield return new WaitUntil(() => serializationCompleted || sw.ElapsedMilliseconds > 5000);

            if(sw.ElapsedMilliseconds > 5000) { 
                UnityEngine.Debug.LogError("Deserialization took too long");
                yield break;
            }

            UnityEngine.Debug.Log($"Deserialized Transform Position: {deserializedTransform.position}, Rotation: {deserializedTransform.rotation}, Scale: {deserializedTransform.localScale}");
            Assert.AreEqual(0f, Vector3.Distance(originalTransform.position, deserializedTransform.position), 1e-5f, "Position did not match after serialization/deserialization");
            Assert.AreEqual(0f, Quaternion.Angle(originalTransform.rotation, deserializedTransform.rotation), 1e-5f, "Rotation did not match after serialization/deserialization");
            Assert.AreEqual(0f, Vector3.Distance(originalTransform.localScale, deserializedTransform.localScale), 1e-5f, "Scale did not match after serialization/deserialization");






            yield return null; // Wait a frame
        }
    }
}
