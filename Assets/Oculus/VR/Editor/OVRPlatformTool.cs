using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using System.Threading;
using System.Diagnostics;
using UnityEngine.Networking;
using System.Net;
using System.Collections;
using System.ComponentModel;
using System.IO;

namespace Assets.Oculus.VR.Editor
{
	public class OVRPlatformTool : EditorWindow
	{
		public enum TargetPlatform
		{
			Rift,
			OculusGoGearVR,
			Quest,
			None,
		};
	
		const string urlPlatformUtil =
			"https://www.oculus.com/download_app/?id=1076686279105243";

		static private Process ovrPlatUtilProcess;
		Vector2 scroll;

		static public string log;

		private static bool activeProcess = false;

		private const float buttonPadding = 5.0f;

		[MenuItem("Oculus/Tools/Oculus Platform Tool")]
		static void Init()
		{
			OVRPlatformTool.log = String.Empty;
			// Get existing open window or if none, make a new one:
			EditorWindow.GetWindow(typeof(OVRPlatformTool));

			if (OVRPlatformToolSettings.TargetPlatform == TargetPlatform.None)
			{
#if UNITY_ANDROID
				OVRPlatformToolSettings.TargetPlatform = TargetPlatform.OculusGoGearVR;
#else
				OVRPlatformToolSettings.TargetPlatform = TargetPlatform.Rift;
#endif
				EditorUtility.SetDirty(OVRPlatformToolSettings.Instance);
			}
			OVRPlugin.SendEvent("oculus_platform_tool", "show_window");
		}

		void OnGUI()
		{
			GUILayout.Label("OVR Platform Tool", EditorStyles.boldLabel);
			this.titleContent.text = "OVR Platform Tool";
			string[] options = new string[]
			{
				"Oculus Rift",
				"Oculus Go | Gear VR",
				"Oculus Quest"
			};
			OVRPlatformToolSettings.TargetPlatform = (TargetPlatform)EditorGUILayout.Popup("Target Oculus Platform", (int)OVRPlatformToolSettings.TargetPlatform, options);
			SetDirtyOnGUIChange();

			GUILayout.BeginVertical(GUILayout.Height(Screen.height/2));
			{
				// Add the UI Form
				GUILayout.FlexibleSpace();
		
				// App ID
				GUIContent AppIDLabel = new GUIContent("Oculus Application ID [?]: ",
					"This AppID will be used when uploading the build.");
				OVRPlatformToolSettings.AppID = MakeTextBox(AppIDLabel, OVRPlatformToolSettings.AppID);

				// App Token
				GUIContent AppTokenLabel = new GUIContent("Oculus App Token [?]: ",
					"You can get your app token from your app's Oculus API Dashboard.");
				OVRPlatformToolSettings.AppToken = MakePasswordBox(AppTokenLabel, OVRPlatformToolSettings.AppToken);

				// Release Channel
				GUIContent ReleaseChannelLabel = new GUIContent("Release Channel [?]: ",
					"Specify the releaes channel of the new build, you can reassign to other channels after upload.");
				OVRPlatformToolSettings.ReleaseChannel = MakeTextBox(ReleaseChannelLabel, OVRPlatformToolSettings.ReleaseChannel);

				// Releaes Note
				GUIContent ReleaseNoteLabel = new GUIContent("Release Note: ");
				OVRPlatformToolSettings.ReleaseNote = MakeTextBox(ReleaseNoteLabel, OVRPlatformToolSettings.ReleaseNote);

				// Platform specific fields
				if (OVRPlatformToolSettings.TargetPlatform == TargetPlatform.Rift)
				{
					GUIContent BuildDirLabel = new GUIContent("Rift Build Directory [?]: ",
						"The full path to the directory containing your Rift build files.");
					OVRPlatformToolSettings.RiftBuildDirectory = MakeTextBox(BuildDirLabel, OVRPlatformToolSettings.RiftBuildDirectory);

					GUIContent BuildVersionLabel = new GUIContent("Build Version [?]: ",
						"The version number shown to users.");
					OVRPlatformToolSettings.RiftBuildVersion = MakeTextBox(BuildVersionLabel, OVRPlatformToolSettings.RiftBuildVersion);

					GUIContent LaunchFileLabel = new GUIContent("Launch File Path [?]: ",
						"The relative path from <BuildPath> to the executable that launches your app.");
					OVRPlatformToolSettings.RiftLaunchFile = MakeTextBox(LaunchFileLabel, OVRPlatformToolSettings.RiftLaunchFile);
				}
				else
				{
					GUIContent ApkPathLabel = new GUIContent("Build APK File Path [?]: ",
						"The full path to the APK file.");
					OVRPlatformToolSettings.ApkBuildPath = MakeTextBox(ApkPathLabel, OVRPlatformToolSettings.ApkBuildPath);
				}

				GUILayout.FlexibleSpace();

				// Add an Upload button
				GUI.enabled = !activeProcess;
				GUIContent btnTxt = new GUIContent("Upload");
				var rt = GUILayoutUtility.GetRect(btnTxt, GUI.skin.button, GUILayout.ExpandWidth(false));
				var btnYPos = rt.center.y; 
				rt.center = new Vector2(EditorGUIUtility.currentViewWidth / 2 - rt.width / 2 - buttonPadding, btnYPos);
				if (GUI.Button(rt, btnTxt, GUI.skin.button))
				{
					OVRPlugin.SendEvent("oculus_platform_tool", "upload");
					OVRPlatformTool.log = String.Empty;
					OnUpload(OVRPlatformToolSettings.TargetPlatform);
				}

				// Add a cancel button
				GUI.enabled = activeProcess;
				btnTxt = new GUIContent("Cancel");
				rt = GUILayoutUtility.GetRect(btnTxt, GUI.skin.button, GUILayout.ExpandWidth(false));
				rt.center = new Vector2(EditorGUIUtility.currentViewWidth / 2 + rt.width / 2 + buttonPadding, btnYPos);
				if (GUI.Button(rt, btnTxt, GUI.skin.button))
				{
					if (EditorUtility.DisplayDialog("Cancel Upload Process", "Are you sure you want to cancel the upload process?", "Yes", "No"))
					{
						if(ovrPlatUtilProcess != null)
						{
							ovrPlatUtilProcess.Kill();
							OVRPlatformTool.log += "Upload process was canceled\n";
						}
					}
				}

				GUI.enabled = true;
				GUILayout.FlexibleSpace();
			}
			GUILayout.EndVertical();

			scroll = EditorGUILayout.BeginScrollView(scroll);
			GUIStyle logBoxStyle = new GUIStyle();
			logBoxStyle.wordWrap = true;
			logBoxStyle.normal.textColor = logBoxStyle.focused.textColor = EditorStyles.label.normal.textColor;
			EditorGUILayout.SelectableLabel(OVRPlatformTool.log, logBoxStyle, GUILayout.Height(position.height - 30));
			EditorGUILayout.EndScrollView();
		}

		private void OnUpload(TargetPlatform targetPlatform)
		{
			OVRPlatformTool.log = String.Empty;
			SetDirtyOnGUIChange();
			ExecuteCommand(targetPlatform);
		}

	  static void ExecuteCommand(TargetPlatform targetPlatform)
		{
			string dataPath = Application.dataPath.ToString();
			var thread = new Thread(delegate () {
				Command(targetPlatform, dataPath);
			});
			thread.Start();
		}

		static void Command(TargetPlatform targetPlatform, string dataPath)
		{
			string toolDataPath = dataPath + "/Oculus/VR/Editor/Tools";
			if (!Directory.Exists(toolDataPath))
			{
				Directory.CreateDirectory(toolDataPath);
			}

			string platformUtil = toolDataPath + "/ovr-platform-util.exe";
			if (!System.IO.File.Exists(platformUtil))
			{
				OVRPlugin.SendEvent("oculus_platform_tool", "provision_util");
				EditorCoroutine downloadCoroutine = EditorCoroutine.Start(ProvisionPlatformUtil(platformUtil));
				while (!downloadCoroutine.GetCompleted()) { }
			}

			string args;
			if (genUploadCommand(targetPlatform, out args))
			{
				activeProcess = true;

				ovrPlatUtilProcess = new Process();
				var processInfo = new ProcessStartInfo(platformUtil, args);

				processInfo.CreateNoWindow = true;
				processInfo.UseShellExecute = false;
				processInfo.RedirectStandardError = true;
				processInfo.RedirectStandardOutput = true;

				ovrPlatUtilProcess.StartInfo = processInfo;
				ovrPlatUtilProcess.EnableRaisingEvents = true;

				ovrPlatUtilProcess.Exited += new EventHandler(
					(s, e) =>
					{
						activeProcess = false;
					}
				);

				ovrPlatUtilProcess.OutputDataReceived += new DataReceivedEventHandler(
					(s, e) =>
					{
						if (e.Data.Length != 0 && !e.Data.Contains("\u001b"))
						{
							OVRPlatformTool.log += e.Data + "\n";
						}
					}
				);
				ovrPlatUtilProcess.ErrorDataReceived += new DataReceivedEventHandler(
					(s, e) =>
					{
						OVRPlatformTool.log += e.Data + "\n";
					}
				);

				ovrPlatUtilProcess.Start();
				ovrPlatUtilProcess.BeginOutputReadLine();
				ovrPlatUtilProcess.BeginErrorReadLine();
			}
		}

		private static bool genUploadCommand(TargetPlatform targetPlatform, out string command)
		{
			bool success = true;
			command = "";

			switch (targetPlatform)
			{
				case TargetPlatform.Rift:
					command = "upload-rift-build";
					break;
				case TargetPlatform.OculusGoGearVR:
					command = "upload-mobile-build";
					break;
				case TargetPlatform.Quest:
					command = "upload-quest-build";
					break;
				default:
					OVRPlatformTool.log += "ERROR: Invalid target platform selected";
					success = false;
					break;
			}

			// Add App ID
			ValidateTextField(AppIDFieldValidator, OVRPlatformToolSettings.AppID, "App ID", ref success);
			command += " --app-id \"" + OVRPlatformToolSettings.AppID + "\"";

			// Add App Token
			ValidateTextField(GenericFieldValidator, OVRPlatformToolSettings.AppToken, "App Token", ref success);
			command += " --app-secret \"" + OVRPlatformToolSettings.AppToken + "\"";

			// Add Platform specific fields
			if (targetPlatform == TargetPlatform.Rift)
			{
				// Add Rift Build Directory
				ValidateTextField(FileDirectoryPathValidator, OVRPlatformToolSettings.RiftBuildDirectory, "Rift Build Directory", ref success);
				command += " --build-dir \"" + OVRPlatformToolSettings.RiftBuildDirectory + "\"";

				// Add Rift Launch File
				ValidateTextField(FileDirectoryPathValidator, OVRPlatformToolSettings.RiftLaunchFile, "Rift Launch File Path", ref success);
				command += " --launch-file \"" + OVRPlatformToolSettings.RiftLaunchFile + "\"";

				// Add Rift Build Version
				ValidateTextField(GenericFieldValidator, OVRPlatformToolSettings.RiftBuildVersion, "Build Version", ref success);
				command += " --version \"" + OVRPlatformToolSettings.RiftBuildVersion + "\"";
			}
			else
			{
				// Add APK Build Path
				ValidateTextField(FileDirectoryPathValidator, OVRPlatformToolSettings.ApkBuildPath, "APK Build Path", ref success);
				command += " --apk \"" + OVRPlatformToolSettings.ApkBuildPath + "\"";
			}

			// Add Release Channel
			ValidateTextField(GenericFieldValidator, OVRPlatformToolSettings.ReleaseChannel, "Release Channel", ref success);
			command += " --channel \"" + OVRPlatformToolSettings.ReleaseChannel + "\"";

			// Add Notes
			if (!string.IsNullOrEmpty(OVRPlatformToolSettings.ReleaseNote))
			{
				string sanatizedReleaseNote = OVRPlatformToolSettings.ReleaseNote;
				sanatizedReleaseNote = sanatizedReleaseNote.Replace("\"", "\"\"");
				command += " --notes \"" + sanatizedReleaseNote + "\"";
			}

			return success;
		}

		// Private delegate for text field validation functions
		private delegate TSuccess FieldValidatorDelegate<in TText, TError, out TSuccess>(TText text, ref TError error);

		// Validate the text using a given field validator function. An error message will be printed if validation fails. Success will ONLY be modified to false if validation fails.
		static void ValidateTextField(FieldValidatorDelegate<string, string, bool> fieldValidator, string fieldText, string fieldName, ref bool success)
		{
			string error = "";
			if (!fieldValidator(fieldText, ref error))
			{
				OVRPlatformTool.log += "ERROR: Please verify that the " + fieldName + " is correct. ";
				OVRPlatformTool.log += string.IsNullOrEmpty(error) ? "\n" : error + "\n";
				success = false;
			}
		}

		// Checks if the text is null or empty
		static bool GenericFieldValidator(string fieldText, ref string error)
		{
			if (string.IsNullOrEmpty(fieldText))
			{
				error = "The field is empty.";
				return false;
			}
			return true;
		}

		// Checks if the App ID contains only numbers
		static bool AppIDFieldValidator(string fieldText, ref string error)
		{
			if(string.IsNullOrEmpty(fieldText))
			{
				error = "The field is empty.";
				return false;
			}
			else if (!Regex.IsMatch(OVRPlatformToolSettings.AppID, "^[0-9]+$"))
			{
				error = "The field contains invalid characters.";
				return false;
			}
			return true;
		}

		// Check if the file path/directory is valid and has no illegal characters
		static bool FileDirectoryPathValidator(string path, ref string error)
		{
			try
			{
				Path.GetFullPath(path);
				return true;
			}
			catch (Exception e)
			{
				error = e.Message.ToString();
				return false;
			}
		}

		void OnInspectorUpdate()
		{
			Repaint();
		}

		private string MakeTextBox(GUIContent label, string variable)
		{
			return GUIHelper.MakeControlWithLabel(label, () => {
				GUI.changed = false;
				var result = EditorGUILayout.TextField(variable);
				SetDirtyOnGUIChange();
				return result;
			});
		}

		private string MakePasswordBox(GUIContent label, string variable)
		{
			return GUIHelper.MakeControlWithLabel(label, () => {
				GUI.changed = false;
				var result = EditorGUILayout.PasswordField(variable);
				SetDirtyOnGUIChange();
				return result;
			});
		}

		private static void SetDirtyOnGUIChange()
		{
			if (GUI.changed)
			{
				EditorUtility.SetDirty(OVRPlatformToolSettings.Instance);
				GUI.changed = false;
			}
		}

		private static IEnumerator ProvisionPlatformUtil(string dataPath)
		{
			using (WWW www = new WWW(urlPlatformUtil))
			{
				UnityEngine.Debug.Log("Started Provisioning Oculus Platform Util");
				float timer = 0;
				float timeOut = 60;
				yield return www;
				while (!www.isDone && timer < timeOut)
				{
					timer += Time.deltaTime;
					if (www.error != null)
					{
						UnityEngine.Debug.Log("Download error: " + www.error);
						break;
					}
					OVRPlatformTool.log = string.Format("Downloading.. {0:P1}", www.progress);
					SetDirtyOnGUIChange();
					yield return new WaitForSeconds(1f);
				}
				if (www.isDone)
				{
					System.IO.File.WriteAllBytes(dataPath, www.bytes);
					OVRPlatformTool.log = "Completed Provisioning Oculus Platform Util";
					SetDirtyOnGUIChange();
				}
			}
		}

		class GUIHelper
		{
			public delegate void Worker();

			static void InOut(Worker begin, Worker body, Worker end)
			{
				try
				{
					begin();
					body();
				}
				finally
				{
					end();
				}
			}

			public static void HInset(int pixels, Worker worker)
			{
				InOut(
					() => {
						GUILayout.BeginHorizontal();
						GUILayout.Space(pixels);
						GUILayout.BeginVertical();
					},
					worker,
					() => {
						GUILayout.EndVertical();
						GUILayout.EndHorizontal();
					}
				);
			}

			public delegate T ControlWorker<T>();
			public static T MakeControlWithLabel<T>(GUIContent label, ControlWorker<T> worker)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(label);

				var result = worker();

				EditorGUILayout.EndHorizontal();
				return result;
			}
		}

		public class EditorCoroutine
		{
			public static EditorCoroutine Start(IEnumerator routine)
			{
				EditorCoroutine coroutine = new EditorCoroutine(routine);
				coroutine.Start();
				return coroutine;
			}

			readonly IEnumerator routine;
		  bool completed;
			EditorCoroutine(IEnumerator _routine)
			{
				routine = _routine;
				completed = false;
			}

			void Start()
			{
				EditorApplication.update += Update;
			}
			public void Stop()
			{
				EditorApplication.update -= Update;
				completed = true;
			}

			public bool GetCompleted()
			{
				return completed;
			}

			void Update()
			{
				if (!routine.MoveNext())
				{
					Stop();
				}
			}
		}

	}
}
