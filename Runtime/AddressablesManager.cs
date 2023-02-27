using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace CustomAddressables
{
    public class AddressablesManager
    {
        public class ResourcePathData
        {
            public string VersionCode;
            public string LocatorId;
            public ResourcePathData() { }
        }

        private static string OriginalRootPath = "";
        private static string CurrentRootPath = "";

        private static List<AsyncOperationHandle> m_LoadedHandle = new();

        private static List<AsyncOperationHandle> m_DontReleaseHandle = new();

        public static bool UpdataResourceVersion { get; private set; }

        /// <summary>
        /// <see cref="AsyncOperationHandle{T}"/> を内包した、アセットの解放を自動的に行うためのカスタムクラス
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class CustomAsyncOperation<T> : CustomYieldInstruction {
            public override bool keepWaiting => IsDone;
            public bool IsDone { get; private set; } = false;
            public AsyncOperationHandle<T> AsyncOperationHandle { get; }

            public event Action<CustomAsyncOperation<T>> Completed = delegate { };

            public CustomAsyncOperation(AsyncOperationHandle<T> handle) {
                AsyncOperationHandle = handle;
                IsDone = false;

                // ロードできなかったらアセット解放してリストからも消す
                handle.Completed += (_handle) => {
                    // 読み込み失敗
                    if (_handle.Status == AsyncOperationStatus.Failed) {
                        Addressables.Release(_handle);

                        if (m_LoadedHandle.Contains(_handle)) {
                            m_LoadedHandle.Remove(_handle);
                        }
                        if (m_DontReleaseHandle.Contains(_handle)) {
                            m_DontReleaseHandle.Remove(_handle);
                        }

                        throw new InvalidOperationException();
                    }

                    Completed?.Invoke(this);
                    IsDone = true;
                };
            }
        }
        private static IEnumerator ChangeRemoteCatalog(ResourcePathData oldPath, string newPath)
        {
            var handle = Addressables.LoadContentCatalogAsync(newPath, true);
            handle.Completed += (_handle) =>
            {
                if (_handle.Status == AsyncOperationStatus.Succeeded)
                {
                    oldPath.LocatorId = _handle.Result.LocatorId;
#if UNITY_EDITOR
                    Debug.Log($"Add RemoteCatalog Paht : {newPath}");
#endif
                }
                else
                {
#if UNITY_EDITOR
                    Debug.Log("通らなかった");
#endif
                }
            };

            yield return handle;
        }

        /// <summary>
        /// Addressablesの非同期アセットロードをするHandleを取得
        /// </summary>
        /// <typeparam name="T">アセットの型</typeparam>
        /// <param name="key">アセットのAddressablesのアドレス</param>
        /// <param name="gameObject">この<see cref="GameObject"/>が破棄されると同時にアセットの解放を行う</param>
        /// <returns>ロードに使用した<see cref="AsyncOperationHandle{T}"/>を内包した<see cref="CustomAsyncOperation{T}"/></returns>
        /// <exception cref="InvalidOperationException">アセットのロードに失敗した時</exception>
        public static CustomAsyncOperation<T> GetOperationHandle<T>(string key, bool isSceneTransitionDontRelease = false) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetAsync<T>(key);

            if (!isSceneTransitionDontRelease)
			{
                m_LoadedHandle.Add(handle);
            }
			else
            {
                m_DontReleaseHandle.Add(handle);
            }

            var customHandle = new CustomAsyncOperation<T>(handle);

            return customHandle;
        }

        /// <summary>
        /// Handleを <see cref="SetDontReleaseHandle"/> で追加したもの以外解放する
        /// </summary>
        public static void ReleaseLoadHandles()
        {
            // アセット解放
            for (int i = 0; i < m_LoadedHandle.Count; ++i)
            {
                Addressables.Release(m_LoadedHandle[i]);
            }

            m_LoadedHandle.Clear();
        }
    }
}