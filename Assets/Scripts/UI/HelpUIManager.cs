using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;
using System.Collections;
using System.Collections.Generic;

public class HelpUIManager : MonoBehaviour
{
    public static HelpUIManager Instance {get; private set;}
    
    [Header("HelpBook Object")]
    public GameObject helpBook;
    
    [Header("UI 구성요소")]
    public Image helpImageDisplay;
    public Button prevButton;
    public Button nextButton;

    [Header("지역화 설정")]
    [Tooltip("Localization Tables 창에서 생성한 에셋 테이블의 이름입니다.")]
    public string assetTableName = "HelpImages"; // ★★★ 이 부분을 실제 생성한 에셋 테이블 이름으로 꼭 변경해주세요! ★★★

    [Tooltip("에셋 테이블에 등록한 도움말 페이지의 Key 목록입니다.")]
    public List<string> helpPageKeys = new List<string>
    {
        "1",
        "2",
        "3",
        "4",
        "5"
    };

    private int currentPageIndex = 0;
    private AsyncOperationHandle<Sprite> currentLoadHandle;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }

    void Start()
    {
        // 버튼이 할당되어 있을 때만 리스너를 추가합니다.
        if (prevButton != null)
        {
            prevButton.onClick.AddListener(ShowPreviousPage);
        }
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(ShowNextPage);
        }
    }

    private void OnEnable()
    {
        // 로컬라이제이션 시스템이 준비될 때까지 기다린 후, 첫 페이지를 로드합니다.
        StartCoroutine(LoadFirstPageAfterInitialization());

        // 언어가 변경될 때마다 이미지를 새로고침하도록 이벤트에 구독합니다.
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 이벤트 구독을 해제하여 메모리 누수를 방지합니다.
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        
        // 로드 중이던 에셋이 있다면 해제합니다.
        ReleaseCurrentImageHandle();
    }

    // 로컬라이제이션 시스템이 준비될 때까지 기다리는 코루틴
    private IEnumerator LoadFirstPageAfterInitialization()
    {
        // LocalizationSettings의 초기화 작업이 끝날 때까지 대기합니다.
        yield return LocalizationSettings.InitializationOperation;
        
        // 초기화가 완료되면 첫 페이지를 로드합니다.
        currentPageIndex = 0;
        LoadAndDisplayImage(currentPageIndex);
    }

    // 언어가 변경되었을 때 호출될 함수
    private void OnLocaleChanged(Locale newLocale)
    {
        // 현재 보고 있는 페이지를 새로운 언어에 맞춰 다시 로드합니다.
        LoadAndDisplayImage(currentPageIndex);
    }

    public void ShowNextPage()
    {
        if (currentPageIndex < helpPageKeys.Count - 1)
        {
            currentPageIndex++;
            LoadAndDisplayImage(currentPageIndex);
        }
    }

    public void ShowPreviousPage()
    {
        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            LoadAndDisplayImage(currentPageIndex);
        }
    }

    private void LoadAndDisplayImage(int pageIndex)
    {
        // 이전에 로드 중이던 이미지가 있다면 핸들을 해제합니다.
        ReleaseCurrentImageHandle();

        if (pageIndex < 0 || pageIndex >= helpPageKeys.Count)
        {
            Debug.LogError("잘못된 페이지 인덱스입니다.");
            return;
        }

        string key = helpPageKeys[pageIndex];

        // 1. LocalizationSettings를 통해 이름으로 에셋 테이블을 가져옵니다. (가장 올바른 방법)
        AsyncOperationHandle<AssetTable> tableHandle = LocalizationSettings.AssetDatabase.GetTableAsync(assetTableName);

        tableHandle.Completed += (op) =>
        {
            if (op.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"에셋 테이블 '{assetTableName}'을 찾을 수 없습니다. 이름이 정확한지, Localization Settings에 포함되었는지 확인하세요.");
                return;
            }

            AssetTable table = op.Result;

            // 2. 가져온 테이블에서 Key를 사용하여 스프라이트를 비동기적으로 로드합니다.
            currentLoadHandle = table.GetAssetAsync<Sprite>(key);
            currentLoadHandle.Completed += (spriteOp) =>
            {
                if (spriteOp.Status == AsyncOperationStatus.Succeeded)
                {
                    helpImageDisplay.sprite = spriteOp.Result;
                    UpdateNavigationButtons();
                }
                else
                {
                    Debug.LogError($"에셋 테이블 '{assetTableName}'에서 Key '{key}'에 해당하는 스프라이트를 로드하는 데 실패했습니다.");
                    helpImageDisplay.sprite = null; // 실패 시 이미지를 비워둡니다.
                }
            };
        };
    }
    
    // 이전/다음 버튼의 활성화 상태를 업데이트합니다.
    private void UpdateNavigationButtons()
    {
        if (prevButton != null)
        {
            prevButton.interactable = (currentPageIndex > 0);
        }
        if (nextButton != null)
        {
            nextButton.interactable = (currentPageIndex < helpPageKeys.Count - 1);
        }
    }

    // 현재 사용 중인 이미지 핸들을 해제하여 메모리를 관리합니다.
    private void ReleaseCurrentImageHandle()
    {
        if (currentLoadHandle.IsValid())
        {
            Addressables.Release(currentLoadHandle);
        }
    }

    public void OnClickHelpIcon()
    {
        OpenBookAndShowPage(0);
    }
    
    public void OpenBookAndShowPage(int pageIndex)
    {
        // pageIndex가 유효한 범위 내에 있는지 확인합니다.
        // helpPageKeys는 0부터 시작하므로, 0보다 작거나 리스트 크기보다 크거나 같으면 안됩니다.
        if (pageIndex < 0 || pageIndex >= helpPageKeys.Count)
        {
            Debug.LogWarning($"요청된 페이지 인덱스({pageIndex})가 유효한 범위를 벗어났습니다. 대신 첫 페이지(0)를 엽니다.");
            pageIndex = 0; // 잘못된 값이면 안전하게 첫 페이지를 엽니다.
        }

        // 도움말 책을 활성화하고 맨 위로 가져옵니다.
        helpBook.SetActive(true);
        helpBook.transform.SetAsLastSibling();

        // helpBook이 활성화되면 OnEnable이 호출되어 첫 페이지를 로드하려고 할 수 있습니다.
        // 하지만 바로 다음에 오는 코드가 원하는 페이지로 덮어쓰므로 괜찮습니다.
        
        // 원하는 페이지 인덱스로 설정하고 이미지를 로드합니다.
        currentPageIndex = pageIndex;
        LoadAndDisplayImage(currentPageIndex);
    }
}