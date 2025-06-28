// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
//
// public class BubbleTTSHandler : MonoBehaviour
// {
//     public AnimalesePlayerLite tts;
//
//     public void Initialize(string presetID, string text)
//     {
//         if (tts == null)
//             tts = GetComponentInChildren<AnimalesePlayerLite>();
//         if (tts == null)
//             return;
//
//         var manager = FindObjectOfType<CharacterPresetManager>();
//         var preset = manager?.presets.Find(p => p.presetID == presetID);
//
//         if (preset != null && tts.useTTS)
//         {
//             tts.voiceFolder = preset.voiceFolder;
//             tts.PlayFromAI(text);
//         }
//     }
// }
