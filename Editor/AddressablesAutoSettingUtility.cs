using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AddressableAssets.AutoSetting {

	#region 例外
	/// <summary>
	/// パスが除外するパスだったときの例外
	/// </summary>
	public class ExcludedPathException : Exception {
		public ExcludedPathException ()
			: base("Is not automatically added because it is included in the excluded path.") {
		}

		public ExcludedPathException (string message)
			: base(message) {
		}

		public ExcludedPathException (string message,Exception innerException)
			: base(message,innerException) {
		}
	}

	public class DoNotApplyAddressablesException : Exception {
		/// <summary>
		/// Addressablesへの設定が出来なかったときの例外
		/// </summary>
		public DoNotApplyAddressablesException ()
			: base("Addressables could not be set because the naming convention is different.") {
		}

		/// <summary>
		/// Addressablesへの設定が出来なかったときの例外
		/// </summary>
		public DoNotApplyAddressablesException (string message)
			: base(message) {
		}

		/// <summary>
		/// Addressablesへの設定が出来なかったときの例外
		/// </summary>
		public DoNotApplyAddressablesException (string message,Exception innerException)
			: base(message,innerException) {
		}
	}
	#endregion

	public class AddressablesAutoSettingUtility {

		private static readonly List<string> m_ExclusionPath = new() {
			"Assets/_pure3/Scripts",        // スクリプトディレクトリ
			"Assets/AddressableAssetsData", // Addressables関連ディレクトリ
			"Assets/BuildReports",          // ビルド結果レポートディレクトリ
			"Assets/Photon",                // Photon関連ディレクトリ
			// ビルド時に省くフォルダ一覧
			"Assets/GraphicAssets",
			"Assets/ORENDA",
		};

		// Addressableの情報取得
		private static AddressableAssetSettings m_Setting = null;
		public static AddressableAssetSettings Setting {
			get {
				if (m_Setting == null) {
					m_Setting = AddressableAssetSettingsDefaultObject.Settings;
				}

				return m_Setting;
			}
		}

		private static Dictionary<string,IAssetConfigurator> m_Configurators = null;
		public static ReadOnlyDictionary<string,IAssetConfigurator> Configurators {
			get {
				if (m_Configurators == null) {
					m_Configurators = Assembly.GetAssembly(typeof(IAssetConfigurator))
										.GetTypes()
										.Where(type => type.GetInterfaces().Contains(typeof(IAssetConfigurator)))
										.ToDictionary(
											type => type.GetCustomAttribute<AssetConfigurationPathAttribute>().Path,
											type => Activator.CreateInstance(type) as IAssetConfigurator
										);
				}

				return new ReadOnlyDictionary<string,IAssetConfigurator>(m_Configurators);
			}
		}

		/// <summary>
		/// アセットが移動された時、再読み込みされた時などにAddressablesの自動設定を行う
		/// </summary>
		/// <param name="assetPath"></param>
		public static void AutoSetting (string assetPath) {
			// 除外するディレクトリかどうか
			if (IsIgnorePath(assetPath)) {
				throw new ExcludedPathException();
			}

			// アセットとして読み込めないもの & DefaultAsset（フォルダなど型がないアセット）は弾く
			var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
			if (asset == null || asset.GetType() == typeof(DefaultAsset)) {
				return;
			}

			var configurator = Configurators.FirstOrDefault(type => assetPath.Contains(type.Key)).Value;

			if (configurator == null) {
				return;
			}

			configurator.Configure(assetPath);
		}

		/// <summary>
		/// ディレクトリ以下にあるアセットをグループに追加する
		/// </summary>
		/// <param name="path">追加するディレクトリ</param>
		/// <param name="ignoreString">無視する文字列</param>
		/// <param name="ignoreType">無視する型</param>
		public static void AutoSettingDirectory (string path,List<string> ignoreString = null,List<Type> ignoreType = null) {
			var fileNames = Directory.GetFiles(path,"*",SearchOption.AllDirectories);

			foreach (var fileName in fileNames) {

				// ファイル名・パスに無視する文字列が含まれている
				if (ignoreString != null && ignoreString.Any(ignore => fileName.Contains(ignore))) {
					continue;
				}

				var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fileName);
				// アセットとして読み込める
				if (asset != null) {
					// 読み込んだアセットの型が無視する型に含まれている
					if (ignoreType != null && ignoreType.Contains(asset.GetType())) {
						continue;
					}

					// 個別アセットパス取得
					string assetPath = AssetDatabase.GetAssetPath(asset);

					try {
						AutoSetting(assetPath);
					}
					catch(DoNotApplyAddressablesException e) {
						string assetName = TrimAssetName(assetPath);
						Debug.LogWarning(e.Message + "\nAsset Name : " + assetName + "\nAsset Path : " + assetPath, asset);
						continue;
					}
					catch (ExcludedPathException) {
						continue;
					}
				}
			}
		}

		/// <summary>
		/// パスが無視するディレクトリを含んでいるか
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static bool IsIgnorePath (string path) {
			// 除外するディレクトリかどうか
			return m_ExclusionPath.Any(exPaths => path.Contains(exPaths));
		}

		/// <summary>
		/// アセットパスからアセットのあるディレクトリを抽出する
		/// </summary>
		/// <param name="assetPath"></param>
		/// <returns></returns>
		public static string TrimAssetDirectory (string assetPath) {
			int dirIndex = assetPath.LastIndexOf("/");
			if (dirIndex == -1) {
				throw new Exception($"string \"{assetPath}\" don't contain \"/\". Please make sure the path is correct.");
			}
			string trim = assetPath.Substring(0,dirIndex);

			return trim;
		}

		/// <summary>
		/// アセットパスからアセット名を抽出する
		/// </summary>
		/// <param name="assetPath">アセットパス</param>
		/// <param name="isIncludeExtension">拡張子を含めるかどうか</param>
		/// <returns></returns>
		public static string TrimAssetName (string assetPath,bool isIncludeExtension = false) {
			int dirIndex = assetPath.LastIndexOf("/");
			if (dirIndex == -1) {
				throw new Exception($"string \"{assetPath}\" don't contain \"/\". Please make sure the path is correct.");
			}
			string trim = assetPath.Substring(dirIndex + 1);

			if (!isIncludeExtension) {
				int extensionIndex = trim.LastIndexOf(".");
				trim = trim.Substring(0,extensionIndex);
			}

			return trim;
		}

		/// <summary>
		/// アセットパスからAssets/_pure3/内で分けられているカテゴリ名を取得する
		/// </summary>
		/// <param name="assetPath"></param>
		/// <returns></returns>
		public static string TrimAssetCategory (string assetPath) {
			string assetsRootPath = "Assets/_pure3/";
			if (!assetPath.Contains(assetsRootPath)) {
				throw new Exception($"Asset not placed in \"{assetsRootPath}\"");
			}

			int categoryIndex = assetPath.IndexOf(assetsRootPath);
			int assetRootLength = assetsRootPath.Length;

			string category = assetPath.Substring(categoryIndex + assetRootLength);

			if (category.Contains("/")) {
				int dirIndex = category.IndexOf("/");
				category = category.Substring(0,dirIndex);
			}

			return category;
		}

		/// <summary>
		/// アセットのAddressablesへの設定を行う
		/// </summary>
		/// <param name="assetPath"></param>
		/// <param name="group"></param>
		/// <param name="label"></param>
		public static void SetAddressables (string assetPath,AddressableAssetGroup group,string[] labels) {
			SetGroup(assetPath,group);
			RenameAsset(assetPath,group);
			SetLabel(assetPath,group,labels);
		}

		public static void SetAddressables (string assetPath,AddressableAssetGroup group,string[] labels,string addName) {
			SetGroup(assetPath,group);
			RenameAsset(assetPath,group,addName);
			SetLabel(assetPath,group,labels);
		}

		/// <summary>
		/// アセットのAddressablesへの設定を行う
		/// </summary>
		/// <param name="assetPath"></param>
		/// <param name="groupName"></param>
		/// <param name="label"></param>
		/// <param name="isForceCreateGroup"></param>
		public static void SetAddressables (string assetPath,string groupName,string[] labels,bool isForceCreateGroup = false) {
			var group = GetGroup(groupName,isForceCreateGroup);
			SetAddressables(assetPath,group,labels);
		}

		public static void SetAddressables (string assetPath,string groupName,string[] labels,string addName,bool isForceCreateGroup = false) {
			var group = GetGroup(groupName,isForceCreateGroup);
			SetAddressables(assetPath,group,labels,addName);
		}

		/// <summary>
		/// Addressablesのグループ設定
		/// </summary>
		/// <param name="assetPath"></param>
		/// <param name="group"></param>
		public static void SetGroup (string assetPath,AddressableAssetGroup group) {
			var guid = AssetDatabase.AssetPathToGUID(assetPath);
			Setting.CreateOrMoveEntry(guid,group);

			AssetDatabase.SaveAssets();
		}

		/// <summary>
		/// Addressablesのラベル設定
		/// </summary>
		/// <param name="assetPath"></param>
		/// <param name="label"></param>
		public static void SetLabel (string assetPath,AddressableAssetGroup group,string[] labels) {
			if (labels == null) {
				return;
			}

			var guid = AssetDatabase.AssetPathToGUID(assetPath);

			var entry = group.GetAssetEntry(guid);

			foreach (string label in labels) {
				entry.SetLabel(label,true,true);
			}

			AssetDatabase.SaveAssets();
		}

		/// <summary>
		/// AddressableAssetEntryのRename
		/// </summary>
		/// <param name="assetPath"></param>
		/// <param name="group"></param>
		public static void RenameAsset(string assetPath, AddressableAssetGroup group) {
			var guid = AssetDatabase.AssetPathToGUID(assetPath);

			var entry = group.GetAssetEntry(guid);

			// Addressableの名前がFilePathだと検索に不都合があるので、FileNameに変換
			entry.address = entry.TargetAsset.name;
		}

		public static void RenameAsset (string assetPath,AddressableAssetGroup group,string addName) {
			var guid = AssetDatabase.AssetPathToGUID(assetPath);

			var entry = group.GetAssetEntry(guid);

			// Addressableの名前がFilePathだと検索に不都合があるので、FileNameに変換
			entry.address = addName + entry.TargetAsset.name;
		}

		/// <summary>
		/// グループ名からグループを取得
		/// </summary>
		/// <param name="groupName">検索するGroupの名前</param>
		/// <param name="isForceCreateGroup">Groupが見つからなかった場合、<see cref="groupName"/>の名前でGroupを作成するかどうか</param>
		/// <returns>見つかったGroup</returns>
		/// <exception cref="NullReferenceException">Groupが見つからない場合（作成する場合を除く）</exception>
		public static AddressableAssetGroup GetGroup (string groupName,bool isForceCreateGroup = false) {
			var group = Setting.groups.FirstOrDefault(g => g.name == groupName);

			if (group == null) {
				if (isForceCreateGroup) {
					group = CreateGroup(groupName);
				}
				else {
					throw new NullReferenceException($"There is no group with name \"{groupName}\".");
				}
			}

			return group;
		}

		public static AddressableAssetGroup SearchGroup(string groupKeyword) {
			var group = Setting.groups.FirstOrDefault(x => x.name.Contains(groupKeyword));

			if (group == null) {
				throw new NullReferenceException($"There is no group with name keyword \"{groupKeyword}\".");
			}

			return group;
		}

		/// <summary>
		/// Groupを作成
		/// </summary>
		/// <param name="groupName">作成するGroupの名前</param>
		/// <returns>作成したGroup</returns>
		public static AddressableAssetGroup CreateGroup (string groupName) {
			// Groupのテンプレートを取得
			var templete = Setting.GetGroupTemplateObject(0) as AddressableAssetGroupTemplate;

			AddressableAssetGroup newGroup = Setting.CreateGroup(groupName,false,false,true,null,templete.GetTypes());
			templete.ApplyToAddressableAssetGroup(newGroup);

			AssetDatabase.SaveAssets();

			return newGroup;
		}

		/// <summary>
		/// 不要なEntryを削除
		/// </summary>
		public static void RemoteEntrys () {
			foreach(var group in Setting.groups) {
				if (group == null) {
					continue;
				}
				var entries = group.entries.ToList();
				foreach(var entry in entries) {
					if (entry.TargetAsset == null) {
						group.RemoveAssetEntry(entry);
					} else {
						// Addressableの名前がFilePathだと検索に不都合があるので、FileNameに変換
						entry.address = entry.TargetAsset.name;
					}
				}
			}
		}

		public static void DeleteGroup () {
			for(int i = 0; i < Setting.groups.Count;++i) {
				if (Setting.groups[i] == null) {
					Setting.groups.RemoveAt(i);
					--i;
				}
			}
		}
	}
}
