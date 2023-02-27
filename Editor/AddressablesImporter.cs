using System;
using UnityEditor;

namespace AddressableAssets.AutoSetting {
	public class AddressablesImporter : AssetPostprocessor {

		/// <summary>
		/// ファイル（アセット）の移動を監視している
		/// </summary>
		/// <param name="importedAssets"></param>
		/// <param name="deletedAssets"></param>
		/// <param name="movedAssets"></param>
		/// <param name="movedFromAssetPaths"></param>
		private static void OnPostprocessAllAssets (string[] importedAssets,string[] deletedAssets,string[] movedAssets,string[] movedFromAssetPaths) {
			// インポートされたもの
			foreach (var import in importedAssets) {
				if (AddressablesAutoSettingUtility.IsIgnorePath(import)) {
					continue;
				}

				try {
					AddressablesAutoSettingUtility.AutoSetting(import);
				}
				catch (NullReferenceException) {
					continue;
				}
				catch (ExcludedPathException) {
					continue;
				}
			}

			// 移動されたファイル
			foreach (var moved in movedAssets) {
				if (AddressablesAutoSettingUtility.IsIgnorePath(moved)) {
					continue;
				}

				try {
					AddressablesAutoSettingUtility.AutoSetting(moved);
				}
				catch (NullReferenceException) {
					continue;
				}
				catch (ExcludedPathException) {
					continue;
				}
			}
		}
	}
}
