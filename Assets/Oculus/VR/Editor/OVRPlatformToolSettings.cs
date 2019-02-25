using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Assets.Oculus.VR.Editor
{
#if UNITY_EDITOR
	[UnityEditor.InitializeOnLoad]
#endif
	public sealed class OVRPlatformToolSettings : ScriptableObject
	{
		public static string AppID
		{
			get { return Instance.appID; }
			set { Instance.appID = value; }
		}

		public static string AppToken
		{
			get { return Instance.appToken; }
			set { Instance.appToken = value; }
		}

		public static string ReleaseNote
		{
			get { return Instance.releaseNote; }
			set { Instance.releaseNote = value; }
		}

		public static string ReleaseChannel
		{
			get { return Instance.releaseChannel; }
			set { Instance.releaseChannel = value; }
		}

		public static string RiftBuildDirectory
		{
			get { return Instance.riftBuildDiretory; }
			set { Instance.riftBuildDiretory = value; }
		}

		public static string ApkBuildPath
		{
			get { return Instance.apkBuildPath; }
			set { Instance.apkBuildPath = value; }
		}

		public static string RiftBuildVersion
		{
			get { return Instance.riftBuildVersion; }
			set { Instance.riftBuildVersion = value; }
		}

		public static string RiftLaunchFile
		{
			get { return Instance.riftLaunchFile; }
			set { Instance.riftLaunchFile = value; }
		}

		public static OVRPlatformTool.TargetPlatform TargetPlatform
		{
			get { return Instance.targetPlatform; }
			set { Instance.targetPlatform = value; }
		}

		[SerializeField]
		private string appID = "";

		[SerializeField]
		private string appToken = "";

		[SerializeField]
		private string releaseNote = "";

		[SerializeField]
		private string releaseChannel = "Beta";

		[SerializeField]
		private string riftBuildDiretory = "";

		[SerializeField]
		private string riftBuildVersion = "";

		[SerializeField]
		private string riftLaunchFile = "";

		[SerializeField]
		private string apkBuildPath = "";

		[SerializeField]
		private OVRPlatformTool.TargetPlatform targetPlatform = OVRPlatformTool.TargetPlatform.None;

		private static OVRPlatformToolSettings instance;
		public static OVRPlatformToolSettings Instance
		{
			get
			{
				if (instance == null)
				{
					instance = Resources.Load<OVRPlatformToolSettings>("OVRPlatformToolSettings");

					if (instance == null)
					{
						instance = ScriptableObject.CreateInstance<OVRPlatformToolSettings>();

						string properPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Resources");
						if (!System.IO.Directory.Exists(properPath))
						{
							UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
						}

						string fullPath = System.IO.Path.Combine(
							System.IO.Path.Combine("Assets", "Resources"),
							"OVRPlatformToolSettings.asset"
						);
						UnityEditor.AssetDatabase.CreateAsset(instance, fullPath);

					}
				}
				return instance;
			}
			set
			{
				instance = value;
			}
		}
	}
}
