using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloseBtn : MonoBehaviour
{
    public GameObject targetToClose;

    public void OnClickClose()
    {
        if (targetToClose != null)
        {
            targetToClose.SetActive(false);
            Debug.Log($"{targetToClose.name} 닫기 실행");
        }
        else
            transform.parent.gameObject.SetActive(false); // fallback
    }
}
