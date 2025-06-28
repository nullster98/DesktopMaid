using System.Collections;
using System.Collections.Generic;
using GemmaCpp;
using UnityEngine;

public class AIBootstrap : MonoBehaviour
{
    [SerializeField] GemmaManager gemmaPrefab;   // 방금 만든 프리팹
    static GemmaManager _gemma;                  // 싱글턴

    void Awake()
    {
        if (_gemma == null)
        {
            _gemma = Instantiate(gemmaPrefab);
            DontDestroyOnLoad(_gemma.gameObject);   // 씬 전환에도 유지
        }
        else
        {
            Destroy(gameObject);  // 이미 있으면 중복 부트스트랩 제거
        }
    }

    public static GemmaManager Instance => _gemma;
}
