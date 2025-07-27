using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;

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
}

public class CharacterGroupManager : MonoBehaviour
{
    public static CharacterGroupManager Instance {get; private set;}
    
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
        };
        allGroups.Add(newGroup);
        Debug.Log($"[GroupManager] Created new group: {newGroup} (ID : {newGroup.groupID})");
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