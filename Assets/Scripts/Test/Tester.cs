using UnityEngine;

public class Tester : MonoBehaviour
{
    public AnimalesePlayerLiteSoundTouch tts;

    void Start()
    {
        tts.PlayFromAI("안녕하세요 너굴너굴 싹십싹십 콩십 메이플하기 귀찮아요");
    }
}