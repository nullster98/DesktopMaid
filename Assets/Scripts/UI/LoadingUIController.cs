using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingUIController : MonoBehaviour
{
    public static LoadingUIController instance;
    
    [Header("UI Components")]
    public GameObject loadingPanel;

    public TMP_Text statusText;
    public Slider progressBar;
    
    private Coroutine progressRoutine;

    private void Awake()
    {
        if(instance == null) instance = this;
        else Destroy(gameObject);
        
        loadingPanel.SetActive(false);
    }
    
    /// <summary>
    /// 로딩창 표시 + 진행바 페이크 시작
    /// </summary>
    public void Show(string message = "VRM 모델을 불러오는 중입니다...")
    {
        loadingPanel.SetActive(true);
        loadingPanel.transform.SetAsLastSibling();
//        statusText.text = message;
        progressBar.value = 0f;

        if (progressRoutine != null)
            StopCoroutine(progressRoutine);
        progressRoutine = StartCoroutine(FakeProgress());
    }

    /// <summary>
    /// 로딩창 종료 + 진행바 초기화
    /// </summary>
    public void Hide()
    {
        if (progressRoutine != null)
            StopCoroutine(progressRoutine);

        progressBar.value = 1f; // 마지막 보정
        loadingPanel.SetActive(false);
    }

    /// <summary>
    /// 약 3초 동안 점진적으로 진행되는 페이크 ProgressBar
    /// </summary>
    private IEnumerator FakeProgress()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 0.33f; // 약 3초 정도 소요
            progressBar.value = Mathf.Clamp01(t);
            yield return null;
        }

        progressBar.value = 1f;
    }
}
