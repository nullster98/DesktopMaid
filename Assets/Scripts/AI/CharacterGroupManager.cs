// --- START OF FILE CharacterGroupManager.cs ---

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using System; // Action과 DateTime을 사용하기 위해 추가

[System.Serializable]
public class CharacterGroup
{
    public string groupID;
    public string parentGroupID;
    public string groupName;
    public string groupConcept;
    public string groupDescription;
    public List<string> memberPresetIDs = new List<string>();
    
    public string groupSymbol_Base64;
    
    public Dictionary<string, string> memberRoles = new Dictionary<string, string>();
    public Dictionary<string, Dictionary<string, string>> memberRelationships 
            = new Dictionary<string, Dictionary<string, string>>();
    
    public string currentContextSummary;
    public List<string> groupLongTermMemories = new List<string>(); // 그룹 장기 기억
    public Dictionary<string, string> groupKnowledgeLibrary = new Dictionary<string, string>(); // 그룹 초장기 기억
    public int lastSummarizedGroupMessageId = 0; // 그룹 대화 요약 위치 추적
    
    // [추가] 메인 리스트 정렬 및 알림 기능을 위해 추가된 필드들
    [NonSerialized] // 이 필드들은 Json 저장에서 제외하고 싶을 때 사용 (선택적)
    public DateTime lastInteractionTime; 
    [NonSerialized]
    public bool HasNotification;
}

public class CharacterGroupManager : MonoBehaviour
{
    public static CharacterGroupManager Instance {get; private set;}
    
    // [추가] 그룹 리스트에 변경이 생겼을 때 다른 시스템에 알리기 위한 이벤트
    public static event Action OnGroupsChanged;
    
    public List<CharacterGroup> allGroups = new List<CharacterGroup>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public CharacterGroup CreateGroup(string name, string concept, string description)
    {
        var newGroup = new CharacterGroup
        {
            groupID = $"group_{System.Guid.NewGuid()}",
            groupName = name,
            groupConcept = concept,
            groupDescription = description,
            lastInteractionTime = DateTime.Now // [추가] 생성 시점을 초기 상호작용 시간으로 설정
        };
        allGroups.Add(newGroup);
        Debug.Log($"[GroupManager] Created new group: {newGroup.groupName} (ID : {newGroup.groupID})");
        
        // [추가] 그룹이 생성되었음을 알림
        OnGroupsChanged?.Invoke();
        
        return newGroup;
    }
    
    public CharacterGroup GetGroup(string groupID)
    {
        return allGroups.FirstOrDefault(g => g.groupID == groupID);
    }
    
    public void DeleteGroup(string groupID)
    {
        var groupToRemove = GetGroup(groupID);
        if (groupToRemove != null)
        {
            // 그룹 삭제 전, 소속된 모든 멤버의 groupID를 초기화
            foreach (var memberID in groupToRemove.memberPresetIDs)
            {
                var preset = CharacterPresetManager.Instance.presets.FirstOrDefault(p => p.presetID == memberID);
                if (preset != null)
                {
                    preset.groupID = null;
                }
            }
            allGroups.Remove(groupToRemove);

            ChatDatabaseManager.Instance.DeleteGroupDatabase(groupID);
            Debug.Log($"[GroupManager] 그룹 삭제: {groupToRemove.groupName}");

            // [추가] 그룹이 삭제되었음을 알림
            OnGroupsChanged?.Invoke();
        }
    }

    public void AddMemberToGroup(string presetID, string groupID)
    {
        var preset = CharacterPresetManager.Instance.presets.FirstOrDefault(p => p.presetID == presetID);
        var group = GetGroup(groupID);

        if (preset == null || group == null)
        {
            Debug.Log("프리셋 또는 그룹을 찾을 수 없습니다.");
            return;
        }
        
        //다른 그룹에 속해있을 경우
        if (!string.IsNullOrEmpty(preset.groupID) && preset.groupID != groupID)
        {
            Debug.Log($"'{preset.characterName}'을(를) 이전 그룹에서 제거합니다...");
            // 경고를 띄우는 대신, 이전 그룹에서 자동으로 제거하는 함수를 먼저 호출합니다.
            RemoveMemberFromGroup(presetID); 
        }
        
        preset.groupID = groupID;
        if (!group.memberPresetIDs.Contains(presetID))
        {
            group.memberPresetIDs.Add(presetID);
        }
        Debug.Log($"[GroupManager] '{preset.characterName}'을(를) '{group.groupName}' 그룹에 추가했습니다.");
        
        var localizedString = new LocalizedString("string Table", "Group_Member_Joined");
        var args = new Dictionary<string, object> { { "CharacterName", preset.characterName } };
        localizedString.Arguments = new object[] { args }; // Dictionary를 배열에 담아 전달
        string systemMessageText = localizedString.GetLocalizedString();
        
        var messageData = new MessageData { type = "system", textContent = systemMessageText };
        string messageJson = JsonUtility.ToJson(messageData);
        
        ChatDatabaseManager.Instance.InsertGroupMessage(groupID, "system", messageJson);
    }
    
    public void RemoveMemberFromGroup(string presetID, bool logMessage = true)
    {
        var preset = CharacterPresetManager.Instance.presets.FirstOrDefault(p => p.presetID == presetID);
        if (preset == null || string.IsNullOrEmpty(preset.groupID)) return;

        var group = GetGroup(preset.groupID);
        if (group != null)
        {
            group.memberPresetIDs.Remove(presetID);
            Debug.Log($"[GroupManager] '{preset.characterName}'을(를) '{group.groupName}' 그룹에서 제거했습니다.");

            if (logMessage)
            {
                var localizedString = new LocalizedString("string Table", "Group_Member_Left");
                var args = new Dictionary<string, object> { { "CharacterName", preset.characterName } };
                localizedString.Arguments = new object[] { args }; // Dictionary를 배열에 담아 전달
                string systemMessageText = localizedString.GetLocalizedString();
                
                var messageData = new MessageData { type = "system", textContent = systemMessageText };
                string messageJson = JsonUtility.ToJson(messageData);

                ChatDatabaseManager.Instance.InsertGroupMessage(preset.groupID, "system", messageJson);
            }
        }
        preset.groupID = null;
    }
    
    // --- 유틸리티 함수 ---
    public List<CharacterPreset> GetGroupMembers(string groupID)
    {
        var group = GetGroup(groupID);
        if(group == null) return new List<CharacterPreset>();

        return CharacterPresetManager.Instance.presets
            .Where(p => group.memberPresetIDs.Contains(p.presetID))
            .ToList();
    }
}
// --- END OF FILE CharacterGroupManager.cs ---