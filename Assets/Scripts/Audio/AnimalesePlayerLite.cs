using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using NPinyin;

[RequireComponent(typeof(AudioSource))]
public class AnimalesePlayerLite : MonoBehaviour
{
    [Header("Activation")] public bool useTTS = true;
    
    [Header("Speed & Pitch")]
    [Range(0.5f, 2f)] public float pitch = 1.2f;
    [Range(0f, 1f)] public float jitter = 0.2f;

    [Header("Vowel Stretch")]
    public int vowelRepeat = 2;

    private AudioSource src;
    private readonly Dictionary<char, AudioClip> clipTable = new();
    private readonly string CHO_TABLE = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    private char[] CHO_ARR;

    public string voiceFolder = "Female1";

    void Awake()
    {
        src = GetComponent<AudioSource>();
        CHO_ARR = CHO_TABLE.ToCharArray();

        for (int i = 0; i < CHO_ARR.Length; i++)
        {
            var clip = Resources.Load<AudioClip>($"TTS/sources/{voiceFolder}/{(i + 1):00}");
            clipTable[CHO_ARR[i]] = clip;
        }

        clipTable[' '] = clipTable['ㅇ'];
    }

    public void Speak(string text)
    {
        StopAllCoroutines();
        StartCoroutine(PlayRoutine(text));
    }

    public void PlayFromAI(string text)
    {
        Debug.Log("PlayFromAI 진입");
        
        if (!useTTS || string.IsNullOrWhiteSpace(text))
            return;
        
        Debug.Log("Speak 바로전");
        Speak(text);
    }

    IEnumerator PlayRoutine(string text)
    {
        foreach (char cho in TextToChos(text))
        {
            if (!clipTable.TryGetValue(cho, out var baseClip) || baseClip == null)
                continue;

            src.clip = baseClip;
            src.pitch = pitch * UnityEngine.Random.Range(1 - jitter, 1 + jitter);
            src.Play();

            float baseDelay = 0.06f;

            float duration = baseDelay;//baseClip.length / src.pitch;
            if (cho == 'ㅇ') duration *= vowelRepeat;

            yield return new WaitForSeconds(duration);
        }
    }

    IEnumerable<char> TextToChos(string txt)
    {
        foreach (var rune in txt)
        {
            if (IsHangulSyllable(rune))
            {
                yield return CHO_ARR[(rune - 0xAC00) / 588];
                continue;
            }

            if (char.IsLetter(rune) && rune < 128)
            {
                yield return EnglishToCho(rune);
                continue;
            }

            if (rune >= 0x3040 && rune <= 0x30FF)
            {
                foreach (char c in JpKanaToCho(rune.ToString())) yield return c;
                continue;
            }

            if (rune >= 0x4E00 && rune <= 0x9FFF)
            {
                foreach (char c in CnHanziToCho(rune.ToString())) yield return c;
                continue;
            }

            yield return 'ㅇ';
        }
    }

    static readonly (string key, char cho)[] EN_MAP = {
        ("CH",'ㅊ'),("SH",'ㅊ'),("TH",'ㄷ'),("PH",'ㅍ'),
        ("B",'ㅂ'),("P",'ㅍ'),("F",'ㅍ'),("D",'ㄷ'),("T",'ㄷ'),("Z",'ㅅ'),("S",'ㅅ'),
        ("K",'ㄱ'),("G",'ㄱ'),("C",'ㅋ'),("M",'ㅁ'),("N",'ㄴ'),("L",'ㄹ'),("R",'ㄹ'),
        ("H",'ㅎ'),("J",'ㅈ'),("Y",'ㅇ'),("W",'ㅇ')
    };
    static char EnglishToCho(char c)
    {
        string up = char.ToUpperInvariant(c).ToString();
        foreach (var (key, cho) in EN_MAP)
            if (key.StartsWith(up)) return cho;
        return 'ㅇ';
    }

    static readonly (string roma, char cho)[] JP_ROMA_CHO = {
        ("ch",'ㅊ'),("sh",'ㅅ'),("ts",'ㅊ'),
        ("ky",'ㅋ'),("gy",'ㄱ'),("ny",'ㄴ'),("hy",'ㅎ'),("my",'ㅁ'),("ry",'ㄹ'),
        ("py",'ㅍ'),("by",'ㅂ'),("j",'ㅈ'),("k",'ㅋ'),("g",'ㄱ'),("s",'ㅅ'),("z",'ㅈ'),
        ("t",'ㄷ'),("d",'ㄷ'),("n",'ㄴ'),("h",'ㅎ'),("b",'ㅂ'),("p",'ㅍ'),("m",'ㅁ'),
        ("y",'ㅇ'),("r",'ㄹ'),("w",'ㅇ')
    };

    static readonly Dictionary<string, string> kanaToRoma = new()
    {
        {"あ","a"},{"い","i"},{"う","u"},{"え","e"},{"お","o"},
        {"か","ka"},{"き","ki"},{"く","ku"},{"け","ke"},{"こ","ko"},
        {"さ","sa"},{"し","shi"},{"す","su"},{"せ","se"},{"そ","so"},
        {"た","ta"},{"ち","chi"},{"つ","tsu"},{"て","te"},{"と","to"},
        {"な","na"},{"に","ni"},{"ぬ","nu"},{"ね","ne"},{"の","no"},
        {"は","ha"},{"ひ","hi"},{"ふ","fu"},{"へ","he"},{"ほ","ho"},
        {"ま","ma"},{"み","mi"},{"む","mu"},{"め","me"},{"も","mo"},
        {"や","ya"},{"ゆ","yu"},{"よ","yo"},
        {"ら","ra"},{"り","ri"},{"る","ru"},{"れ","re"},{"ろ","ro"},
        {"わ","wa"},{"を","wo"},{"ん","n"},
        {"が","ga"},{"ぎ","gi"},{"ぐ","gu"},{"げ","ge"},{"ご","go"},
        {"ざ","za"},{"じ","ji"},{"ず","zu"},{"ぜ","ze"},{"ぞ","zo"},
        {"だ","da"},{"ぢ","ji"},{"づ","zu"},{"で","de"},{"ど","do"},
        {"ば","ba"},{"び","bi"},{"ぶ","bu"},{"べ","be"},{"ぼ","bo"},
        {"ぱ","pa"},{"ぴ","pi"},{"ぷ","pu"},{"ぺ","pe"},{"ぽ","po"},
        {"きゃ","kya"},{"きゅ","kyu"},{"きょ","kyo"},
        {"しゃ","sha"},{"しゅ","shu"},{"しょ","sho"},
        {"ちゃ","cha"},{"ちゅ","chu"},{"ちょ","cho"},
        {"にゃ","nya"},{"にゅ","nyu"},{"にょ","nyo"},
        {"ひゃ","hya"},{"ひゅ","hyu"},{"ひょ","hyo"},
        {"みゃ","mya"},{"みゅ","myu"},{"みょ","myo"},
        {"りゃ","rya"},{"りゅ","ryu"},{"りょ","ryo"},
        {"ぎゃ","gya"},{"ぎゅ","gyu"},{"ぎょ","gyo"},
        {"じゃ","ja"},{"じゅ","ju"},{"じょ","jo"},
        {"びゃ","bya"},{"びゅ","byu"},{"びょ","byo"},
        {"ぴゃ","pya"},{"ぴゅ","pyu"},{"ぴょ","pyo"},
    };

    static string ToBasicRomaji(string kana)
    {
        var result = "";
        for (int i = 0; i < kana.Length;)
        {
            string pair = i + 1 < kana.Length ? kana.Substring(i, 2) : kana.Substring(i, 1);
            if (kanaToRoma.TryGetValue(pair, out var romaji))
            {
                result += romaji;
                i += pair.Length;
            }
            else
            {
                string single = kana.Substring(i, 1);
                result += kanaToRoma.ContainsKey(single) ? kanaToRoma[single] : single;
                i += 1;
            }
        }
        return result;
    }

    static IEnumerable<char> JpKanaToCho(string kana)
    {
        string roma = ToBasicRomaji(kana).ToLower();
        int i = 0;
        while (i < roma.Length)
        {
            string onset = "";
            foreach (var (key, cho) in JP_ROMA_CHO)
            {
                if (i + key.Length <= roma.Length && roma.Substring(i, key.Length) == key)
                {
                    onset = key;
                    yield return cho;
                    i += key.Length;
                    break;
                }
            }

            if (onset == "")
            {
                yield return 'ㅇ';
                i++;
            }

            while (i < roma.Length && "aeiou".IndexOf(roma[i]) >= 0) i++;
        }
    }

    static readonly (string onset, char cho)[] CN_ONSET_CHO = {
        ("zh",'ㅈ'),("ch",'ㅊ'),("sh",'ㅅ'),
        ("j",'ㅈ'),("q",'ㅊ'),("x",'ㅅ'),("z",'ㅈ'),("c",'ㅊ'),("s",'ㅅ'),
        ("b",'ㅂ'),("p",'ㅍ'),("m",'ㅁ'),("f",'ㅍ'),
        ("d",'ㄷ'),("t",'ㄷ'),("n",'ㄴ'),("l",'ㄹ'),
        ("g",'ㄱ'),("k",'ㅋ'),("h",'ㅎ'),("r",'ㄹ')
    };
    static IEnumerable<char> CnHanziToCho(string han)
    {
        string[] syls = Pinyin.GetInitials(han).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (string syl in syls)
        {
            string s = syl.ToLower();
            foreach (var (onset, cho) in CN_ONSET_CHO)
                if (s.StartsWith(onset)) { yield return cho; goto next; }
            yield return 'ㅇ';
        next: ;
        }
    }

    static bool IsHangulSyllable(char c) => c >= 0xAC00 && c <= 0xD7A3;
}
