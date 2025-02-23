using RDR2;
using RDR2.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using RDR2.Native;
using System.Linq;
using RDR2.Math;

namespace Lockhart {
	public class Lockhart : Script {
		/* Debug */

		private string _debug;
		private bool showDebug = false;
		private List<Keys> pressedKeys = new List<Keys>();

		/* Mod */

		private int inPhotoDeadEyeTill = 0;
		private int restorePlayer = 0;
		private float defaultBlendRatio = 0.0f;
		private int photosPerRoll = 6;
		private int currentPhotosRemaining = 6;

		private int filmAdvanceDuration = 3000;
		private int filmReloadDuration = 10000;
		private int filmAdvanceTimer = 0;
		private bool filmIsAdvanced = true;
		private List<int> cameraWindAudioTimer;

		private int flashDuration = 600;
		private int flashDurationTimer = 0;
		private int flashDelay = 400;
		private int flashDelayTimer = 0;
		private bool waitingForFlash = false;

		private bool useFlash = false;
		private int flashPower = 0;


		/* Sound stuff */
		private bool is_soundset_playing = false;
		private Dictionary<string, int> soundTimeouts = new Dictionary<string, int>();

		public Lockhart() {
			Audio.PrepareSoundset("CAMERA_SOUNDSET");
			Audio.PrepareSoundset("MASON_PHOTO_SOUNDSET");
			defaultBlendRatio = RDR2.Native.Function.Call<float>(0x8517D4A6CA8513ED, PLAYER.PLAYER_PED_ID());

			Tick += OnTick;
			KeyDown += OnKeyDown;
			KeyUp += OnKeyUp;
			Interval = 1;
		}

		private void OnTick(object sender, EventArgs evt) {

			_debug = string.Empty;

			AddDebugMessage(() => $"Blend ratio; {defaultBlendRatio}\n");

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
					PED.SET_PED_MAX_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 3f);
					PED.SET_PED_MIN_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 3f);
				} else {
					PED.SET_PED_MAX_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 1f);
					PED.SET_PED_MIN_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 0f);
				}
			} else {
				PED.SET_PED_MAX_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 3f);
				PED.SET_PED_MIN_MOVE_BLEND_RATIO(PLAYER.PLAYER_PED_ID(), 0f);
			}

			if (waitingForFlash && flashDelayTimer <= Game.GameTime) {
				AddDebugMessage(() => $"Waiting for flash: {waitingForFlash}\n");
				SnapShot();
				waitingForFlash = false;
			}

			/* HUD */

			if (Game.Player.Ped.Weapons.Current.Name == "WEAPON_KIT_CAMERA") {
				AddDebugMessage(() => $"{currentPhotosRemaining}/{photosPerRoll}\n");
				AddDebugMessage(() => $"Camera Ready: {filmIsAdvanced}\n");
				AddDebugMessage(() => $"Use Flash: {useFlash}\n");
				AddDebugMessage(() => $"Flash Power: {flashPower}");
				AddDebugMessage(() => $"Res: {RDR2.UI.Screen.Width} x {RDR2.UI.Screen.Width}\n");

				//TextElement CameraHud = new TextElement($"{currentPhotosRemaining}", new PointF(RDR2.UI.Screen.Width - 50f, 50.0f), 0.55f, Color.WhiteSmoke, Alignment.Right, true, true);
				//TextElement flashStatus = new TextElement($"{useFlash}", new PointF(RDR2.UI.Screen.Width - 50f, 100.0f), 0.25f, useFlash ? Color.Yellow : Color.DarkGray, Alignment.Right, true, true);
				//CameraHud.Draw();
				//flashStatus.Draw();

				ContainerElement hudy = new ContainerElement(new Point((int)RDR2.UI.Screen.Width - 250, 50), new SizeF(200f, 300f));
				hudy.Items.Add(new TextElement($"{(currentPhotosRemaining > 0 ? currentPhotosRemaining.ToString() : "R")}", new Point(135, 0), 0.55f, filmIsAdvanced ? Color.WhiteSmoke : Color.DarkGray, Alignment.Right, true, true));
				hudy.Items.Add(new TextElement($"F{flashPower}", new Point(160, 0), 0.55f, flashPower > 0 ? Color.Yellow : Color.DarkGray, Alignment.Right, true, true));
				hudy.Draw();


			}

			AddDebugMessage(() => $"Keys: {string.Join(", ", pressedKeys)}\n");

			//Game.DisableControlThisFrame(eInputType.CameraHandheldUse);
			Game.DisableControlThisFrame(eInputType.CameraSelfie);

			if (!filmIsAdvanced) {
				if (filmAdvanceTimer <= Game.GameTime) {
					EndFilmAdvance();
				} else if (cameraWindAudioTimer.Any(t => t <= Game.GameTime)) {
					PlaySound("CAMERA_SOUNDSET", "Wind_On_Film", 500);
					cameraWindAudioTimer.Remove(cameraWindAudioTimer.FirstOrDefault(t => t <= Game.GameTime));
				}
			}



			Flash();




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


		private void OnKeyDown(object sender, KeyEventArgs e) {
			if (!pressedKeys.Contains(e.KeyCode)) {
				pressedKeys.Add(e.KeyCode);
			}


			if (Game.Player.Ped.Weapons.Current.Name == "WEAPON_KIT_CAMERA" && e.KeyCode == Keys.F) {
				if (flashPower == 3) {
					flashPower = 0;
				} else {
					flashPower++;
				}
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
				if (filmIsAdvanced) {

					if (flashPower > 0) {
						flashDurationTimer = Game.GameTime + flashDuration;
						PlaySound("MASON_PHOTO_SOUNDSET", "Camera_Flash", 600);
						waitingForFlash = true;
						flashDelayTimer = Game.GameTime + flashDelay;
						/* Snap will happen in main loop */
					} else {
						SnapShot();
					}
				} else {
					RDR2.UI.Screen.DisplaySubtitle($"Camera not ready");
				}
			}
		}

		private void OnKeyUp(object sender, KeyEventArgs e) {
			pressedKeys.Remove(e.KeyCode);
		}


		private void BeginFilmAdvanceOrReload() {
			currentPhotosRemaining--;
			filmIsAdvanced = false;
			if (currentPhotosRemaining > 0) {
				filmAdvanceTimer = Game.GameTime + filmAdvanceDuration;
				cameraWindAudioTimer = new List<int> {
					Game.GameTime + 1000,
					Game.GameTime + 2000
				};
			} else {
				filmAdvanceTimer = Game.GameTime + filmReloadDuration;
				cameraWindAudioTimer = new List<int> {
					Game.GameTime + 1000,
					Game.GameTime + 2000,
					Game.GameTime + 3000,
					Game.GameTime + 4000,
					Game.GameTime + 5000,
					Game.GameTime + 6000,
					Game.GameTime + 7000,
					Game.GameTime + 8000,
					Game.GameTime + 9000
				};
			}
		}

		private void EndFilmAdvance() {
			filmIsAdvanced = true;
			PlaySound("CAMERA_SOUNDSET", "Collapse_Camera", 300);
			if (currentPhotosRemaining == 0) {
				currentPhotosRemaining = photosPerRoll;
			}
		}

		private void SnapShot() {
			if (showDebug) { 
			RDR2.UI.Screen.DisplaySubtitle($"Snapshot at {Game.GameTime}");
			}
			Game.Player.Ped.IsVisible = false;
			RDR2.Native.GRAPHICS.FREE_MEMORY_FOR_HIGH_QUALITY_PHOTO();
			RDR2.Native.GRAPHICS.BEGIN_TAKE_HIGH_QUALITY_PHOTO();
			RDR2.Native.GRAPHICS.SAVE_HIGH_QUALITY_PHOTO(0);
			restorePlayer = Game.GameTime + 50;
			inPhotoDeadEyeTill = 0;
			if (flashPower == 0) {
				PlaySound("MASON_PHOTO_SOUNDSET", "CAMERA_CLICK", 300);
			}

			BeginFilmAdvanceOrReload();
		}

		private void Flash() {
			if (flashPower > 0 && flashDurationTimer >= Game.GameTime) {
				GRAPHICS.DRAW_LIGHT_WITH_RANGE(ENTITY.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS(PLAYER.PLAYER_PED_ID(), .5f, 0f, 1f), 229, 198, 137, 25f * flashPower, 20f);
			}
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
