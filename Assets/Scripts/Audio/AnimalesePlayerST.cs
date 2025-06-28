// /*  AnimalesePlayerST.cs  ------------------------------------------
//  *  Unity-only Animalese synthesizer (KR/EN/JP/CN)
//  *  Speed ↑ with partial pitch-correction (SoundTouch)
//  * --------------------------------------------------------------- */
//
// using System;
// using System.Collections.Generic;
// using System.IO;
// using UnityEngine;
// using SoundTouch;                 // SoundTouch.Net.dll
// using WanaKanaShaapu;             // KakasiSharp.dll
// using NPinyin;                   // NPinyin.dll
//
// [RequireComponent(typeof(AudioSource))]
// public class AnimalesePlayerST : MonoBehaviour
// {
//     /* === 공개 파라미터 =============================== */
//     [Header("Speed & Tone")]
//     [Range(1f, 4f)] public float baseRate = 4f;   // 평균 배속
//     [Range(0f, .5f)] public float jitter   = .4f; // 속도 ±
//     [Range(0f, 1f)] public float alpha     = .5f; // 피치 보정 (0=없음,1=완전)
//
//     [Header("Vowel Stretch")]
//     public int vowelRepeat = 2;                   // ㅇ 길이 배수
//
//     /* === 내부 변수 =================================== */
//     AudioSource src;
//     readonly Dictionary<char, AudioClip> clipTable = new();
//     readonly string tempDir =
//         Path.Combine(Application.temporaryCachePath, "animalese");
//
//     // 초성 인덱스 테이블
//     const string CHO_TABLE = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
//     readonly char[] CHO_ARR = CHO_TABLE.ToCharArray();
//
//     /* ---------- 초기화 ---------- */
//     void Awake()
//     {
//         if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
//
//         src = GetComponent<AudioSource>();
//
//         // Resources/TTS/Chos/01.wav ~ 20.wav
//         for (int i = 0; i < 20; i++)
//         {
//             var clip = Resources.Load<AudioClip>($"TTS/Chos/{(i + 1):00}");
//             clipTable[CHO_ARR[i]] = clip;
//         }
//         // 공백용 무음도 하나 더
//         clipTable[' '] = clipTable[CHO_ARR[11]]; // 'ㅇ' 재사용
//     }
//
//     /* ---------- Public API ---------- */
//     public void Speak(string text)
//     {
//         StopAllCoroutines();
//         StartCoroutine(PlayRoutine(text));
//     }
//
//     /* ---------- 메인 코루틴 ---------- */
//     System.Collections.IEnumerator PlayRoutine(string text)
//     {
//         foreach (char cho in TextToChos(text))
//         {
//             if (!clipTable.TryGetValue(cho, out var baseClip) || baseClip == null)
//                 continue;
//
//             float rate = baseRate * UnityEngine.Random.Range(1 - jitter, 1 + jitter);
//
//             // 1) baseClip → temp wav
//             string wavIn  = Path.Combine(tempDir, "in.wav");
//             string wavOut = Path.Combine(tempDir, "out.wav");
//             SaveClipToWav(baseClip, wavIn);
//
//             // 2) SoundTouch 처리
//             ProcessSoundTouch(wavIn, wavOut, rate);
//
//             // 3) 로드 & 재생
//             var clip = WavUtility.ToAudioClip(wavOut, "aniClip");
//             src.clip = clip;
//             src.Play();
//
//             float dur = clip.length * (cho == 'ㅇ' ? vowelRepeat : 1);
//             yield return new WaitForSeconds(dur);
//
//             Destroy(clip);
//             SafeDelete(wavIn); SafeDelete(wavOut);
//         }
//     }
//
//     /* ---------- SoundTouch 래퍼 ---------- */
//     void ProcessSoundTouch(string inPath, string outPath, float rate)
//     {
//         float stretch = Mathf.Pow(rate,  alpha);
//         float pitchRt = Mathf.Pow(rate, 1f - alpha);
//         float pitchSt = 12f * Mathf.Log(pitchRt, 2);
//
//         var st = new SoundTouchWrapper();
//         
//             st.SetSampleRate(44100);
//             st.SetChannels(1);
//             st.SetTempo(1f / stretch); // tempo = 1/speed
//             st.SetPitchSemiTones(pitchSt);
//         
//
//         st.PutSamplesFromFile(inPath);
//         st.Flush();
//         float[] samples = st.ReceiveSamples();
//
//         // float32 → WAV
//         File.WriteAllBytes(outPath, WavUtility.FromAudioFloat(samples, 1, 44100));
//     }
//
//     /* ---------- 텍스트 → 초성 시퀀스 ---------- */
//     IEnumerable<char> TextToChos(string txt)
//     {
//         foreach (var rune in txt)
//         {
//             // 1) 한글 음절
//             if (IsHangulSyllable(rune))
//             {
//                 yield return CHO_ARR[(rune - 0xAC00) / 588];
//                 continue;
//             }
//
//             // 2) 영어 알파벳
//             if (char.IsLetter(rune) && rune < 128)
//             {
//                 yield return EnglishToCho(rune);
//                 continue;
//             }
//
//             // 3) 일본어 히라가나/가타카나
//             if (rune >= 0x3040 && rune <= 0x30FF)
//             {
//                 foreach (char c in JpKanaToCho(rune.ToString())) yield return c;
//                 continue;
//             }
//
//             // 4) 중국어 한자
//             if (rune >= 0x4E00 && rune <= 0x9FFF)
//             {
//                 foreach (char c in CnHanziToCho(rune.ToString())) yield return c;
//                 continue;
//             }
//
//             // 5) 기타 → 모음 ‘ㅇ’
//             yield return 'ㅇ';
//         }
//     }
//
//     /* ---- 영어: 첫 자·자음 군 매핑 ---- */
//     static readonly (string key, char cho)[] EN_MAP = {
//         ("CH",'ㅊ'),("SH",'ㅊ'),("TH",'ㄷ'),("PH",'ㅍ'),
//         ("B",'ㅂ'),("P",'ㅍ'),("F",'ㅍ'),
//         ("D",'ㄷ'),("T",'ㄷ'),("Z",'ㅅ'),("S",'ㅅ'),
//         ("K",'ㄱ'),("G",'ㄱ'),("C",'ㅋ'),
//         ("M",'ㅁ'),("N",'ㄴ'),("L",'ㄹ'),("R",'ㄹ'),
//         ("H",'ㅎ'),("J",'ㅈ'),("Y",'ㅇ'),("W",'ㅇ')
//     };
//     static char EnglishToCho(char c)
//     {
//         string up = char.ToUpperInvariant(c).ToString();
//         foreach (var (key, cho) in EN_MAP)
//             if (key.StartsWith(up)) return cho;
//         return 'ㅇ';
//     }
//
//     /* ---- 일본어: 히라/카타 → Hepburn 로마자 → 초성 ---- */
//     static readonly (string roma, char cho)[] JP_ROMA_CHO = {
//         ("ch",'ㅊ'),("sh",'ㅅ'),("ts",'ㅊ'),
//         ("ky",'ㅋ'),("gy",'ㄱ'),("ny",'ㄴ'),("hy",'ㅎ'),("my",'ㅁ'),("ry",'ㄹ'),
//         ("py",'ㅍ'),("by",'ㅂ'),("j",'ㅈ'),
//         ("k",'ㅋ'),("g",'ㄱ'),("s",'ㅅ'),("z",'ㅈ'),("t",'ㄷ'),("d",'ㄷ'),
//         ("n",'ㄴ'),("h",'ㅎ'),("b",'ㅂ'),("p",'ㅍ'),("m",'ㅁ'),
//         ("y",'ㅇ'),("r",'ㄹ'),("w",'ㅇ')
//     };
//     static IEnumerable<char> JpKanaToCho(string kana)
//     {
//         // KakasiSharp: Hiragana/Katakana → Hepburn 로마자
//         string roma = KakasiConverter.ToHepburn(kana).ToLower();
//         // onset 분리 (첫 자음 군만)
//         int i = 0;
//         while (i < roma.Length)
//         {
//             string onset = "";
//             foreach (var (key, cho) in JP_ROMA_CHO)
//                 if (roma.StartsWith(key, i))
//                 { onset = key; yield return cho; i += key.Length; break; }
//             if (onset == "") { yield return 'ㅇ'; i++; }          // 모음만
//             // 뒤 모음 스킵
//             while (i < roma.Length && "aeiou".IndexOf(roma[i]) >= 0) i++;
//         }
//     }
//
//     /* ---- 중국어: NPinyin → onset ---- */
//     static readonly (string onset, char cho)[] CN_ONSET_CHO = {
//         ("zh",'ㅈ'),("ch",'ㅊ'),("sh",'ㅅ'),
//         ("j",'ㅈ'),("q",'ㅊ'),("x",'ㅅ'),
//         ("z",'ㅈ'),("c",'ㅊ'),("s",'ㅅ'),
//         ("b",'ㅂ'),("p",'ㅍ'),("m",'ㅁ'),("f",'ㅍ'),
//         ("d",'ㄷ'),("t",'ㄷ'),("n",'ㄴ'),("l",'ㄹ'),
//         ("g",'ㄱ'),("k",'ㅋ'),("h",'ㅎ'),
//         ("r",'ㄹ')
//     };
//     static IEnumerable<char> CnHanziToCho(string han)
//     {
//         string[] syls = Pinyin.ConvertSentence(han).Split(' ', StringSplitOptions.RemoveEmptyEntries);
//         foreach (string syl in syls)
//         {
//             string s = syl.ToLower();
//             foreach (var (onset, cho) in CN_ONSET_CHO)
//                 if (s.StartsWith(onset))
//                 { yield return cho; goto next; }
//             yield return 'ㅇ';
//         next: ;
//         }
//     }
//
//     /* ---------- 유틸 ---------------- */
//     static bool IsHangulSyllable(char c) => c >= 0xAC00 && c <= 0xD7A3;
//
//     static void SaveClipToWav(AudioClip clip, string path)
//     {
//         float[] data = new float[clip.samples];
//         clip.GetData(data, 0);
//         File.WriteAllBytes(path, WavUtility.FromAudioFloat(data, clip.channels, clip.frequency));
//     }
//     static void SafeDelete(string p) { if (File.Exists(p)) File.Delete(p); }
// }
