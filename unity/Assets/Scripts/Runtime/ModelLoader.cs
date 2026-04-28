using System.Threading.Tasks;
using UnityEngine;
using GLTFast;

namespace QuestBase.Runtime
{
    public class ModelLoader : MonoBehaviour
    {
        /// <summary>
        /// Load a glTF/GLB model from a local file path and parent it to this transform.
        /// </summary>
        public async Task<GameObject> LoadFromFile(string filePath)
        {
            var gltf = new GltfImport();
            bool success = await gltf.Load($"file://{filePath}");

            if (!success)
            {
                Debug.LogError($"[ModelLoader] Failed to load: {filePath}");
                return null;
            }

            var container = new GameObject($"Model_{System.IO.Path.GetFileNameWithoutExtension(filePath)}");
            container.transform.SetParent(transform);
            await gltf.InstantiateMainSceneAsync(container.transform);

            Debug.Log($"[ModelLoader] Loaded: {filePath}");
            return container;
        }

        /// <summary>
        /// Load a glTF/GLB model from a URL and parent it to this transform.
        /// </summary>
        public async Task<GameObject> LoadFromUrl(string url)
        {
            var gltf = new GltfImport();
            bool success = await gltf.Load(url);

            if (!success)
            {
                Debug.LogError($"[ModelLoader] Failed to load: {url}");
                return null;
            }

            var container = new GameObject($"Model_{System.IO.Path.GetFileNameWithoutExtension(url)}");
            container.transform.SetParent(transform);
            await gltf.InstantiateMainSceneAsync(container.transform);

            Debug.Log($"[ModelLoader] Loaded from URL: {url}");
            return container;
        }
    }
}
