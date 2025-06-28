using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NPinyin; // 중국어 초성 추출용

[RequireComponent(typeof(AudioSource))]
public class AnimalesePlayerLiteSoundTouch : MonoBehaviour
{
    [Header("TTS Options")]
    public bool useTTS = true;
    public float tempo = 1.0f;
    public float pitch = 0.0f;
    public float interval = 0.5f;
    [Range(0f, 1f)] public float jitter = 0.2f;
    public int vowelRepeat = 2;
    public string voiceFolder = "Female1";
    public AudioSource audioSource;

    private static readonly string[] CHO_ARR = new string[]
    {
        "ㄱ", "ㄲ", "ㄴ", "ㄷ", "ㄸ", "ㄹ", "ㅁ",
        "ㅂ", "ㅃ", "ㅅ", "ㅆ", "ㅇ", "ㅈ",
        "ㅉ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ"
    };

    private Dictionary<string, AudioClip> clipTable = new Dictionary<string, AudioClip>();

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        Debug.Log($"[TTS] AudioSource 상태 - volume: {audioSource.volume}, mute: {audioSource.mute}");
        
        for (int i = 0; i < CHO_ARR.Length; i++)
        {
            string key = CHO_ARR[i];
            var clip = Resources.Load<AudioClip>($"TTS/sources/{voiceFolder}/{(i + 1):00}");
            if (clip != null)
            {
                clipTable[key] = clip;
            }
            else
            {
                Debug.LogWarning($"[TTS] Clip not found: {key} → TTS/sources/{voiceFolder}/{(i + 1):00}");
            }
        }
    }

    public void PlayFromAI(string text)
    {
        if (!useTTS) return;
        StartCoroutine(PlayMergedCoroutine(TextToChos(text)));
    }

    private IEnumerator PlayCoroutine(string choText)
    {
        using (SoundTouchWrapper st = new SoundTouchWrapper())
        {
            st.SetSampleRate(44100);
            st.SetChannels(1);
            st.SetTempo(tempo);
            st.SetPitch(pitch);

            foreach (char ch in choText)
            {
                if (!clipTable.TryGetValue(ch.ToString(), out var clip)) continue;

                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                float pitchVariation = pitch + UnityEngine.Random.Range(-jitter, jitter);
                st.SetPitch(pitchVariation);

                for (int i = 0; i < vowelRepeat; i++)
                {
                    st.PutSamples(samples, samples.Length);
                    float[] output = new float[clip.samples * 2];
                    int received = st.ReceiveSamples(output, output.Length);
                    
                    Debug.Log($"[TTS] {ch}: {samples.Length} samples → {output.Length} output samples");


                    if (received > 0)
                    {
                        float[] truncated = new float[received];
                        Array.Copy(output, truncated, received);

                        var outClip = AudioClip.Create("Processed", received, 1, 44100, false);
                        outClip.SetData(truncated, 0);
                        audioSource.clip = outClip;
                        audioSource.PlayOneShot(outClip);
                        
                        yield return new WaitForSeconds(clip.length * interval);
                    }
                }
            }
        }
    }
    
    private IEnumerator PlayMergedCoroutine(string choText)
    {
        List<float> allSamples = new List<float>();

        using (SoundTouchWrapper st = new SoundTouchWrapper())
        {
            st.SetSampleRate(44100);
            st.SetChannels(1);
            st.SetTempo(tempo);
            st.SetPitch(pitch);

            foreach (char ch in choText)
            {
                if (!clipTable.TryGetValue(ch.ToString(), out var clip)) continue;

                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                float pitchVariation = pitch + UnityEngine.Random.Range(-jitter, jitter);
                st.SetPitch(pitchVariation);

                for (int i = 0; i < vowelRepeat; i++)
                {
                    st.PutSamples(samples, samples.Length);
                    float[] output = new float[clip.samples * 2];
                    int received = st.ReceiveSamples(output, output.Length);
                    if (received > 0)
                    {
                        float[] truncated = new float[received];
                        Array.Copy(output, truncated, received);
                        allSamples.AddRange(truncated);
                    }
                }
            }

            if (allSamples.Count > 0)
            {
                float[] finalSamples = allSamples.ToArray();
                var outClip = AudioClip.Create("Merged", finalSamples.Length, 1, 44100, false);
                outClip.SetData(finalSamples, 0);
                audioSource.clip = outClip;
                audioSource.Play();
            }
        }

        yield return null;
    }

    private string TextToChos(string text)
    {
        string result = "";
        foreach (char c in text)
        {
            if (IsHangul(c))
            {
                int unicode = c - 0xAC00;
                int choIndex = unicode / 588;
                if (choIndex >= 0 && choIndex < CHO_ARR.Length)
                    result += CHO_ARR[choIndex];
            }
            else if (IsEnglish(c)) result += EnglishToCho(c);
            else if (IsKana(c)) result += JpKanaToCho(c.ToString());
            else if (IsHanzi(c)) result += CnHanziToCho(c.ToString());
        }
        return result;
    }

    private string EnglishToCho(char c)
    {
        c = char.ToLower(c);
        if ("a".Contains(c)) return "ㄱ";
        if ("b".Contains(c)) return "ㄲ";
        if ("c".Contains(c)) return "ㄴ";
        if ("d".Contains(c)) return "ㄷ";
        if ("e".Contains(c)) return "ㄸ";
        if ("f".Contains(c)) return "ㄹ";
        if ("g".Contains(c)) return "ㅁ";
        if ("h".Contains(c)) return "ㅂ";
        if ("i".Contains(c)) return "ㅃ";
        if ("j".Contains(c)) return "ㅅ";
        if ("k".Contains(c)) return "ㅆ";
        if ("l".Contains(c)) return "ㅇ";
        if ("m".Contains(c)) return "ㅈ";
        if ("n".Contains(c)) return "ㅉ";
        if ("o".Contains(c)) return "ㅊ";
        if ("p".Contains(c)) return "ㅋ";
        if ("q".Contains(c)) return "ㅌ";
        if ("r".Contains(c)) return "ㅍ";
        if ("s".Contains(c)) return "ㅎ";
        if ("tuvwxyz".Contains(c)) return "ㅇ"; // 여분 문자 → 중립적인 ㅇ
        return "ㅇ"; // 예외 fallback
    }

    private string JpKanaToCho(string kana)
    {
        string roma = ToBasicRomaji(kana);
        if (string.IsNullOrEmpty(roma)) return "ㅇ";
        return EnglishToCho(roma[0]);
    }

    private string ToBasicRomaji(string kana)
    {
        Dictionary<string, string> table = new Dictionary<string, string>
        {
            {"あ","a"}, {"い","i"}, {"う","u"}, {"え","e"}, {"お","o"},
            {"か","ka"}, {"き","ki"}, {"く","ku"}, {"け","ke"}, {"こ","ko"},
            {"さ","sa"}, {"し","shi"}, {"す","su"}, {"せ","se"}, {"そ","so"},
            {"た","ta"}, {"ち","chi"}, {"つ","tsu"}, {"て","te"}, {"と","to"},
            {"な","na"}, {"に","ni"}, {"ぬ","nu"}, {"ね","ne"}, {"の","no"},
            {"は","ha"}, {"ひ","hi"}, {"ふ","fu"}, {"へ","he"}, {"ほ","ho"},
            {"ま","ma"}, {"み","mi"}, {"む","mu"}, {"め","me"}, {"も","mo"},
            {"や","ya"}, {"ゆ","yu"}, {"よ","yo"},
            {"ら","ra"}, {"り","ri"}, {"る","ru"}, {"れ","re"}, {"ろ","ro"},
            {"わ","wa"}, {"を","wo"}, {"ん","n"}
        };
        return table.TryGetValue(kana, out var roma) ? roma : "";
    }

    private string CnHanziToCho(string han)
    {
        string pinyin = Pinyin.GetPinyin(han);
        if (string.IsNullOrEmpty(pinyin)) return "ㅇ";
        return EnglishToCho(pinyin[0]);
    }

    private bool IsHangul(char c) => c >= 0xAC00 && c <= 0xD7A3;
    private bool IsEnglish(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
    private bool IsKana(char c) => (c >= 0x3040 && c <= 0x30FF);
    private bool IsHanzi(char c) => c >= 0x4E00 && c <= 0x9FFF;
}
