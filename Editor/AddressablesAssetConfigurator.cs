using System;

namespace AddressableAssets.AutoSetting {

	/// <summary>
	/// <see cref="IAssetConfigurator"/>インターフェースを実装する際、対象となるパスを設定する属性
	/// </summary>
	[AttributeUsage(AttributeTargets.Class,AllowMultiple = false,Inherited = false)]
	public sealed class AssetConfigurationPathAttribute : Attribute {

		public string Path { get; }

		public AssetConfigurationPathAttribute (string path) {
			Path = path;
		}
	}

	/// <summary>
	/// アセットに対してAddressablesのグループ・ラベルを設定するインターフェース<br/>
	/// アセットのパスを指定するため、<see cref="AssetConfigurationPathAttribute">AddressablePath</see>属性の指定をしてください。
	/// </summary>
	public interface IAssetConfigurator {
		public void Configure (string assetPath);
	}	
}
