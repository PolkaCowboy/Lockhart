using RDR2;
using RDR2.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Threading;
using RDR2.Native;
using System.IO;
using System.Linq;

namespace Lockhart {
	public class Lockhart : Script {
		/* Debug */

		private string _debug;
		private bool showDebug = true;
		private List<Keys> pressedKeys = new List<Keys>();

		/* Mod */

		private int inPhotoDeadEyeTill = 0;
		private int restorePlayer = 0;
		private float defaultBlendRatio = 0.0f;
		

		/* Sound stuff */
		private bool is_soundset_playing = false;
		private Dictionary<string, int> soundTimeouts = new Dictionary<string, int>();

		public Lockhart() {

			defaultBlendRatio = RDR2.Native.Function.Call<float>(0x8517D4A6CA8513ED, PLAYER.PLAYER_PED_ID());

			Tick += OnTick;
			KeyDown += OnKeyDown;
			KeyUp += OnKeyUp;
			Interval = 1;
		}

		private void OnTick(object sender, EventArgs evt) {
			Audio.PrepareSoundset("CAMERA_SOUNDSET");
			_debug = string.Empty;

			AddDebugMessage(() => $"Blend ratio; {defaultBlendRatio}");

			if (Game.GameTime >= restorePlayer) {
				Game.Player.Ped.IsVisible = true;
			}

			if (inPhotoDeadEyeTill >= Game.GameTime) {
				Game.TimeScale = 0.25f;
			} else {
				Game.TimeScale = 1f;
			}

			defaultBlendRatio = RDR2.Native.Function.Call<float>(0x8517D4A6CA8513ED, PLAYER.PLAYER_PED_ID());
			if (Game.Player.Ped.Weapons.Current.Name == "WEAPON_KIT_CAMERA" && pressedKeys.Any(k => k == Keys.W || k == Keys.A || k == Keys.D || k == Keys.S)) {
				if (pressedKeys.Contains(Keys.ShiftKey)) {
					AddDebugMessage(() => $"sprinting");
					PED.SET_PED_MAX_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 3f);
					PED.SET_PED_MIN_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 3f);
				} else {
					AddDebugMessage(() => $"Moving");
					PED.SET_PED_MAX_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 1f);
					PED.SET_PED_MIN_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 0f);
				}
			} else {
				PED.SET_PED_MAX_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 3f);
				PED.SET_PED_MIN_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 0f);
			}



			AddDebugMessage(() => $"Keys: {string.Join(", ", pressedKeys)}\n");

			//Game.DisableControlThisFrame(eInputType.CameraHandheldUse);
			Game.DisableControlThisFrame(eInputType.CameraSelfie);


			foreach (var soundTimeout in soundTimeouts) {
				AddDebugMessage(() => $"Timeout: {soundTimeout.Key}: {soundTimeout.Value} <= {Game.GameTime}\n");
				if (soundTimeout.Value <= Game.GameTime) {
					EndSound(soundTimeout.Key);
				}
			}


			if (showDebug) {
				TextElement textElement = new TextElement($"{_debug}", new PointF(200.0f, 200.0f), 0.35f);
				textElement.Draw();
			}

		}


		private string soundset_ref = "ABIGAIL_3_SOUNDSET";
		private string soundset_name = "Camera_Flash";

		private void OnKeyDown(object sender, KeyEventArgs e) {
			if (!pressedKeys.Contains(e.KeyCode)) {
				pressedKeys.Add(e.KeyCode);
			}

			//(Keyboard Only)
			//RDR2.UI.Screen.DisplaySubtitle($"{e.KeyCode}");
			//Use X-Mouse to send keyboard combo with every middle mouse click
			if (pressedKeys.Contains(Keys.ShiftKey) && e.KeyCode == Keys.OemMinus && Game.Player.Ped.Weapons.Current.Name == "WEAPON_KIT_CAMERA") {
				/*
				  ["Photo_Mode_Sounds"] = {
					  "back",
					  "effects",
					  "filter_left",
					  "filter_right",
					  "hide_hud",
					  "lens_down",
					  "reset",
					  "take_photo",
					  "lens_down",
					  "lens_up",
					},
				 * */

				if (inPhotoDeadEyeTill >= Game.GameTime) {
					inPhotoDeadEyeTill = 0;
				} else {
					inPhotoDeadEyeTill = Game.GameTime + 600;
				}

			}

			/*
		["CAMERA_SOUNDSET"] = {
      "Change_Expression",
      "Change_Pose",
      "CLICK",
      "Collapse_Camera",
      "DOF_Change",
      "Expand_Camera",
      "Place_Tripod",
      "Remove_Tripod",
      "Take_Photo",
      "Wind_On_Film",
      "Zoom_In",
      "Zoom_Out",
    },
			*/

			//Shift + ?
			//Use X-Mouse to send keyboard combo with every left mouse click
			if (pressedKeys.Contains(Keys.ShiftKey) && e.KeyCode == Keys.OemQuestion && Game.Player.Ped.Weapons.Current.Name == "WEAPON_KIT_CAMERA") {
				Game.Player.Ped.IsVisible = false;
				RDR2.Native.GRAPHICS.FREE_MEMORY_FOR_HIGH_QUALITY_PHOTO();
				RDR2.Native.GRAPHICS.BEGIN_TAKE_HIGH_QUALITY_PHOTO();
				RDR2.Native.GRAPHICS.SAVE_HIGH_QUALITY_PHOTO(0);
				restorePlayer = Game.GameTime + 50;
				inPhotoDeadEyeTill = 0;
				PlaySound("CAMERA_SOUNDSET", "CLICK", 300);

			}
		}

		private void OnKeyUp(object sender, KeyEventArgs e) {
			pressedKeys.Remove(e.KeyCode);
		}

		private void PlaySound(string soundset_ref, string soundset_name, int soundTimeOutLength) {
			if (!is_soundset_playing && !string.IsNullOrEmpty(soundset_name)) {
				int counter_i = 1;
				while (!Audio.PrepareSoundset(soundset_ref) && counter_i <= 300) // load soundset
				{
					counter_i++;
					Thread.Sleep(0);
				}

				if (Audio.PrepareSoundset(soundset_ref)) {
					// PLAY SOUND FROM POSITION:
					//var ped_coords = GetEntityCoords(ped);
					//var forwardVector = GetEntityForwardVector(ped);
					//float x = ped_coords.X + forwardVector.X * 15.0f;
					//float y = ped_coords.Y + forwardVector.Y * 15.0f;
					//float z = ped_coords.Z - 1.0f;
					//Citizen.InvokeNative(0xCCE219C922737BFA, soundset_name, x, y, z, soundset_ref, true, 0, true, 0); // PLAY_SOUND_FROM_POSITION
					is_soundset_playing = true;

					// OR PLAY SOUND FROM ENTITY:
					RDR2.Native.Function.Call<bool>(0x6FB1DA3CA9DA7D90, soundset_name, PLAYER.PLAYER_PED_ID(), soundset_ref, true, 0, 0); // PLAY_SOUND_FROM_ENTITY
					soundTimeouts.Add(soundset_ref, Game.GameTime + soundTimeOutLength);
				}
			} else {
				EndSound(soundset_ref);
			}
		}

		private void EndSound(string soundset_ref) {
			RDR2.Native.Function.Call(0x531A78D6BF27014B, soundset_ref); // stop soundset (required, otherwise new soundsets can fail to load)
			is_soundset_playing = false;
			soundTimeouts = new Dictionary<string, int>();
		}

		public void AddDebugMessage(Func<string> message) {
			if (showDebug) {
				_debug += message();
			}
		}

	}
}
