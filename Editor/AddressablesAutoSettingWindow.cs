using UnityEditor;
using UnityEditor.AddressableAssets;

namespace AddressableAssets.AutoSetting {
	public class AddressablesAutoSettingWindow : EditorWindow {

		[MenuItem("Tools/Addressables/Auto Setting Reload",priority = 2070)]
		public static void Reload () {

			if (AddressableAssetSettingsDefaultObject.Settings == null) {
				return;
            }

			// 同じ名前を設定できないので、不要なAssetの削除から走らせる
			AddressablesAutoSettingUtility.RemoteEntrys();
			// Assetをマップする際に無効なGroupがあると問題がでる
			AddressablesAutoSettingUtility.DeleteGroup();

			var configurators = AddressablesAutoSettingUtility.Configurators;

			foreach (var type in configurators.Keys) {
				try {
					AddressablesAutoSettingUtility.AutoSettingDirectory(type);
				}
				catch (DoNotApplyAddressablesException) {
					continue;
				}
			}

			// Build時に無効なGroupがあるとBuildできない
			AddressablesAutoSettingUtility.DeleteGroup();
		}
	}
}
