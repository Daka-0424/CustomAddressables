using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build;
using System.IO;
using System.Text;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditorInternal;
using System;
using AddressableAssets.AutoSetting;

namespace AddressableAssets.Build {
	public class AddressablesBuildEditor : EditorWindow {
		public static string HTTPSPath => AddressableBuildParameter.Instance.RemotePath;
		public static string VersionCode => AddressableBuildParameter.Instance.VersionCode;

		static List<AddressableAssetGroup> AddressableAssetGroups => AddressableAssetSettingsDefaultObject.Settings.groups;

		private ResourceType[] resourceTypes = default;
		private int[] versionCodes = default;

		private bool m_DetailSettings = false;

		[MenuItem("Tools/Addressables/Assets Build")]
		static void ShowWindow () {
			EditorWindow.GetWindow<AddressablesBuildEditor>();
		}

		private void OnEnable () {
			// Versionと書かれたAddressableProfileを検索
			if (AddressableAssetSettingsDefaultObject.Settings != null) {
				resourceTypes = AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetVariableNames().Where(x => x.Contains("Version")).Select(x => new ResourceType(x)).ToArray();
				versionCodes = new int[resourceTypes.Length];
			}
		}

		private void OnGUI () {
			GUILayout.Label("AddressablesBuildEditor",EditorStyles.largeLabel);
			GUILayout.Space(5);

			if (AddressableAssetSettingsDefaultObject.Settings != null) {
				// Versionと書かれたAddressableProfile分BuildButtonを表示する
				foreach (ResourceType resourceType in resourceTypes) {
					resourceType.ResourceVersion = EditorGUILayout.TextField(resourceType.ResourceName, resourceType.ResourceVersion);
					if (GUILayout.Button($"Build {resourceType.ResourceName.Replace("Version", "")}")) {
						BuildAddressables(resourceType.ResourceName.Replace("Version", ""), resourceType.ResourceVersion);
					}
					GUILayout.Space(5);
				}
			}

			// AddressableのCashClear
			GUILayout.Space(5);
			if (GUILayout.Button("Set Game Build Settings")) {
				SetGameBuildSettings();
			}

			// HTTPなどの設定
			GUILayout.Space(5);
			m_DetailSettings = EditorGUILayout.Foldout(m_DetailSettings,"詳細設定");
			if (m_DetailSettings) {
				GUILayout.Space(5);
				AddressableBuildParameter.Instance.BaseRemotePath = EditorGUILayout.TextField("Resource Storage Path ",AddressableBuildParameter.Instance.BaseRemotePath);
				GUI.enabled = !string.IsNullOrEmpty(AddressableBuildParameter.Instance.BaseRemotePath) && AddressableBuildParameter.Instance.BaseRemotePath.Contains("{0}");
				GUILayout.Space(5);
				AddressableBuildParameter.Instance.RemoteTargetEnv = EditorGUILayout.TextField("Target Env ",AddressableBuildParameter.Instance.RemoteTargetEnv);
				GUI.enabled = true;
			}
		}
		/// <summary>
		/// Jenkins用
		/// </summary>
		public static void BuildAddressables () {
			string[] args = System.Environment.GetCommandLineArgs();
			string assetType = args[1];
			string versionCode = args[2];
			string targetEnv = args[3];

            AddressablesAutoSettingWindow.Reload();

			BuildAddressables(assetType,versionCode,targetEnv);
		}
		/// <summary>
		/// AddressableBuild
		/// </summary>
		/// <param name="assetType">ClientMasterなど</param>
		/// <param name="versionCode">0.0.1など</param>
		/// <param name="targetEnv">dev01など</param>
		private static void BuildAddressables(string assetType,string versionCode,string targetEnv = null) {
			string buildPathId = "";
			string loadPathId = "";

			AddressableAssetSettings.CleanPlayerContent();
			BuildCache.PurgeCache(false);
			Caching.ClearCache();

			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			AddressableAssetProfileSettings profileSettings = settings.profileSettings;

			foreach(string profileName in profileSettings.GetVariableNames().Where(x => x.IndexOf(assetType,StringComparison.OrdinalIgnoreCase) >= 0 && !x.Contains("Version"))) {
				if (profileName.Contains("BuildPath")) {
					buildPathId = profileSettings.GetValueByName(profileSettings.GetProfileId("default"), profileName);
				} else if (profileName.Contains("LoadPath")) {
					loadPathId = profileSettings.GetValueByName(profileSettings.GetProfileId("default"), profileName);
				}
			}
			
			AddressableBuildParameter.Instance.VersionCode = versionCode;

			if (!string.IsNullOrEmpty(targetEnv)) {
				AddressableBuildParameter.Instance.RemoteTargetEnv = targetEnv;
			}

			bool isFirstGroup = true;
			settings.BuildRemoteCatalog = true;
			foreach(AddressableAssetGroup assetGroup in AddressableAssetGroups) {
				var build = assetGroup.GetSchema<BundledAssetGroupSchema>();
				if (build == null) {
					continue;
				}
				if (build.LoadPath.Id == loadPathId) {
					build.IncludeInBuild = true;
					if (isFirstGroup) {
						isFirstGroup = false;
						settings.RemoteCatalogBuildPath.SetVariableById(settings,build.BuildPath.Id);
						settings.RemoteCatalogLoadPath.SetVariableById(settings,build.LoadPath.Id);
						settings.DefaultGroup = assetGroup;
					}
				}
				else {
					build.IncludeInBuild = false;
				}
			}

			try {
				AddressableAssetSettings.BuildPlayerContent();
			}
			catch(Exception e) {
				EditorApplication.Exit(1);
			}

			AddressableAssetSettings.CleanPlayerContent();
			BuildCache.PurgeCache(false);
			Caching.ClearCache();
		}
		/// <summary>
		/// AddressableのCashClear用
		/// </summary>
		public static void SetGameBuildSettings () {
			string buildPathId = "";
			string loadPathId = "";

			if (AddressableAssetSettingsDefaultObject.Settings == null) {
				return;
            }

			AddressableAssetSettings.CleanPlayerContent();
			BuildCache.PurgeCache(false);
			Caching.ClearCache();

			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			AddressableAssetProfileSettings profileSettings = settings.profileSettings;

			foreach (string profileName in profileSettings.GetVariableNames().Where(x => x.IndexOf("Local", StringComparison.OrdinalIgnoreCase) >= 0 && !x.Contains("Version"))) {
				if (profileName.Contains("BuildPath")) {
					buildPathId = profileSettings.GetValueByName(profileSettings.GetProfileId("default"), profileName);
				}
				else if (profileName.Contains("LoadPath")) {
					loadPathId = profileSettings.GetValueByName(profileSettings.GetProfileId("default"), profileName);
				}
			}

			bool isFirstGroup = true;
			settings.BuildRemoteCatalog = false;

			foreach (AddressableAssetGroup assetGroup in AddressableAssetGroups) {
				var build = assetGroup.GetSchema<BundledAssetGroupSchema>();
				if (build == null) {
					continue;
				}
				if (build.LoadPath.Id == loadPathId) {
					build.IncludeInBuild = true;
					if (isFirstGroup) {
						isFirstGroup = false;
						settings.RemoteCatalogBuildPath.SetVariableById(settings,build.BuildPath.Id);
						settings.RemoteCatalogLoadPath.SetVariableById(settings,build.LoadPath.Id);
						settings.DefaultGroup = assetGroup;
					}
				}
				else {
					build.IncludeInBuild = false;
				}
			}
		}
	}

	public class ResourceType {
		[SerializeField]
		private string resourceName = default;
		[SerializeField]
		private string resourceVersion = default;

		public ResourceType (string ResourceName) {
			resourceName = ResourceName;
			resourceVersion = "";
		}
		public string ResourceName => resourceName;
		public string ResourceVersion {
			get => resourceVersion;
			set { resourceVersion = value; }
		}
	}
	public class AddressableBuildParameter{
		private const string SaveFilePath = "ProjectSettings/AddressableBuildParameter.asset";

		[SerializeField]
		private string baseRemotePath = "";
		[SerializeField]
		private string remoteTargetEnv = "";
		[SerializeField]
		private string versionCode = "";

		private static AddressableBuildParameter instance;

		public static AddressableBuildParameter Instance {
			get {
				if (instance == null) {
					instance = new AddressableBuildParameter();
					if (File.Exists(SaveFilePath)) {
						var reader = new StreamReader(SaveFilePath,Encoding.UTF8);
						var json = reader.ReadToEnd();

						reader.Close();

						JsonUtility.FromJsonOverwrite(json,instance);

						if (instance == null) {
							instance = new AddressableBuildParameter();
						}
					}
				}
				return instance;
			}
		}
		public string BaseRemotePath {
			get => instance.baseRemotePath;
			set {
				if (instance.baseRemotePath != value) {
					instance.baseRemotePath = value;
					Save();
				}
			}
		}
		public string RemoteTargetEnv {
			get => instance.remoteTargetEnv;
			set {
				if (instance.remoteTargetEnv != value) {
					instance.remoteTargetEnv = value;
					Save();
				}
			}
		}
		public string RemotePath {
			get {
				if (instance.baseRemotePath.Contains("{0}")) {
					return string.Format(instance.baseRemotePath,instance.remoteTargetEnv);
				} else {
					return instance.baseRemotePath;
				}
			}
		}
		public string VersionCode {
			get => instance.versionCode;
			set {
				if(instance.versionCode != value) {
					instance.versionCode = value;
					Save();
				}
			}
		}

		private static void Save () {
			var json = JsonUtility.ToJson(instance);
			var write = new StreamWriter(SaveFilePath,false,Encoding.UTF8);

			write.Write(json);

			write.Close();
		}
	}
}