using UnityEngine;

// 이 스크립트는 버튼에 부착하여 HelpUIManager의 특정 페이지를 열도록 설계되었습니다.
public class HelpPageOpener : MonoBehaviour
{
    [Tooltip("열고 싶은 도움말 페이지 번호를 입력하세요. (1부터 시작)")]
    [Min(1)]
    public int targetPageNumber = 1;

    public void OpenHelpToSpecificPage()
    {
        if (HelpUIManager.Instance == null)
        {
            Debug.LogError("씬에 HelpUIManager가 존재하지 않습니다!", this.gameObject);
            return;
        }

        int pageIndex = targetPageNumber - 1;

        HelpUIManager.Instance.OpenBookAndShowPage(pageIndex);
    }
}