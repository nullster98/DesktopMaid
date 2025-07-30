// --- START OF FILE IMainListItem.cs ---

using System;
using UnityEngine;

/// <summary>
/// 메인 UI 리스트에 표시될 아이템의 종류를 정의합니다.
/// </summary>
public enum ListItemType
{
    Character,
    Group
}

/// <summary>
/// 메인 UI 리스트에 표시될 모든 아이템(캐릭터, 그룹)이 공통으로 가져야 할 속성과 기능을 정의하는 인터페이스입니다.
/// </summary>
public interface IMainListItem
{
    /// <summary>
    /// 아이템의 고유 ID (프리셋 ID 또는 그룹 ID)
    /// </summary>
    string ID { get; }

    /// <summary>
    /// UI에 표시될 이름
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// UI에 표시될 프로필 이미지
    /// </summary>
    Sprite ProfileSprite { get; }

    /// <summary>
    /// 아이템의 종류 (캐릭터 또는 그룹)
    /// </summary>
    ListItemType Type { get; }

    /// <summary>
    /// 마지막 상호작용 시간. 리스트 정렬의 핵심 기준이 됩니다.
    /// </summary>
    DateTime LastInteractionTime { get; set; }

    /// <summary>
    /// 새로운 알림(메시지)이 있는지 여부
    /// </summary>
    bool HasNotification { get; set; }

    /// <summary>
    /// 전달된 GameObject를 기반으로 자신의 UI를 업데이트합니다.
    /// </summary>
    /// <param name="uiObject">이 데이터를 표시할 UI 게임 오브젝트</param>
    void UpdateDisplay(GameObject uiObject);

    /// <summary>
    /// 아이템이 클릭되었을 때 호출될 동작입니다.
    /// </summary>
    void OnClick();
}

// --- END OF FILE IMainListItem.cs ---