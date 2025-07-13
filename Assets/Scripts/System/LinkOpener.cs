using UnityEngine;

public class LinkOpener : MonoBehaviour
{
    /// <summary>
    /// 지정된 URL을 브라우저에서 엽니다.
    /// 이 함수는 Unity의 버튼 OnClick() 이벤트에서 호출하기 위해 public으로 선언되었습니다.
    /// </summary>
    /// <param name="url">열고 싶은 웹페이지 주소</param>
    public void OpenLink(string url)
    {
        // URL이 비어있지 않은지 확인합니다.
        if (!string.IsNullOrEmpty(url))
        {
            // Application.OpenURL을 호출하여 사용자의 기본 웹 브라우저에서 링크를 엽니다.
            Application.OpenURL(url);
            Debug.Log($"다음 링크를 열었습니다: {url}");
        }
        else
        {
            Debug.LogWarning("열려고 하는 URL이 비어있습니다!");
        }
    }
}