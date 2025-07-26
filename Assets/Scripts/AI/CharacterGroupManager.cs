using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// 모든 캐릭터 그룹의 데이터를 관리하는 싱글턴 매니저.
/// 그룹의 생성, 삭제, 멤버 관리 및 관련 데이터 조회를 담당합니다.
/// </summary>
public class CharacterGroupManager : MonoBehaviour
{
    public static CharacterGroupManager Instance { get; private set; }
    
    // 프로그램 내에 존재하는 모든 그룹의 정보를 담는 리스트
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
    
    #region Public API - Group Management

    /// <summary>
    /// 새로운 그룹을 생성하고 관리 목록에 추가합니다.
    /// </summary>
    /// <returns>생성된 새로운 CharacterGroup 객체</returns>
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
        Debug.Log($"[GroupManager] 새로운 그룹 생성: {name} (ID: {newGroup.groupID})");
        return newGroup;
    }
    
    /// <summary>
    /// 그룹 ID를 사용하여 특정 그룹 객체를 찾습니다.
    /// </summary>
    /// <returns>찾은 그룹 객체. 없으면 null을 반환합니다.</returns>
    public CharacterGroup GetGroup(string groupID)
    {
        return allGroups.FirstOrDefault(g => g.groupID == groupID);
    }
    
    /// <summary>
    /// 특정 그룹을 삭제하고, 관련된 모든 멤버의 소속 정보를 초기화하며, 채팅 DB도 삭제합니다.
    /// </summary>
    public void DeleteGroup(string groupID)
    {
        CharacterGroup groupToRemove = GetGroup(groupID);
        if (groupToRemove != null)
        {
            // 그룹이 삭제되기 전, 소속된 모든 멤버의 groupID를 null로 초기화
            foreach (string memberID in groupToRemove.memberPresetIDs)
            {
                CharacterPreset preset = CharacterPresetManager.Instance.GetPreset(memberID);
                if (preset != null)
                {
                    preset.groupID = null;
                }
            }
            
            allGroups.Remove(groupToRemove);
            ChatDatabaseManager.Instance.DeleteGroupDatabase(groupID);
            Debug.Log($"[GroupManager] 그룹 삭제 완료: {groupToRemove.groupName}");
        }
    }
    
    #endregion
    
    #region Public API - Member Management

    /// <summary>
    /// 특정 캐릭터를 지정된 그룹에 추가합니다. 이미 다른 그룹에 속해있다면 자동으로 탈퇴시킵니다.
    /// </summary>
    public void AddMemberToGroup(string presetID, string groupID)
    {
        CharacterPreset preset = CharacterPresetManager.Instance.GetPreset(presetID);
        CharacterGroup group = GetGroup(groupID);

        if (preset == null || group == null)
        {
            Debug.LogWarning($"[GroupManager] 멤버 추가 실패: 프리셋({presetID}) 또는 그룹({groupID})을 찾을 수 없습니다.");
            return;
        }
        
        // 만약 캐릭터가 이미 다른 그룹에 속해 있다면, 이전 그룹에서 먼저 자동으로 제거
        if (!string.IsNullOrEmpty(preset.groupID) && preset.groupID != groupID)
        {
            Debug.Log($"[GroupManager] '{preset.characterName}'을(를) 이전 그룹에서 자동 탈퇴 처리합니다.");
            RemoveMemberFromGroup(presetID, logMessage: false); // 이전 그룹 탈퇴 메시지는 남기지 않음
        }
        
        preset.groupID = groupID;
        if (!group.memberPresetIDs.Contains(presetID))
        {
            group.memberPresetIDs.Add(presetID);
        }
        Debug.Log($"[GroupManager] '{preset.characterName}' 님을 '{group.groupName}' 그룹에 추가했습니다.");
        
        // 시스템 메시지 ("~님이 입장했습니다")를 DB에 기록
        LogSystemMessageToGroupChat(groupID, "Group_Member_Joined", preset.characterName);
    }
    
    /// <summary>
    /// 특정 캐릭터를 현재 소속된 그룹에서 제거합니다.
    /// </summary>
    /// <param name="presetID">제거할 캐릭터의 프리셋 ID</param>
    /// <param name="logMessage">채팅방에 탈퇴 메시지를 남길지 여부</param>
    public void RemoveMemberFromGroup(string presetID, bool logMessage = true)
    {
        CharacterPreset preset = CharacterPresetManager.Instance.GetPreset(presetID);
        if (preset == null || string.IsNullOrEmpty(preset.groupID)) return;

        CharacterGroup group = GetGroup(preset.groupID);
        if (group != null)
        {
            group.memberPresetIDs.Remove(presetID);
            Debug.Log($"[GroupManager] '{preset.characterName}' 님을 '{group.groupName}' 그룹에서 제거했습니다.");

            if (logMessage)
            {
                LogSystemMessageToGroupChat(group.groupID, "Group_Member_Left", preset.characterName);
            }
        }
        // 캐릭터의 소속 그룹 정보를 완전히 초기화
        preset.groupID = null;
    }
    
    #endregion
    
    #region Utility Methods

    /// <summary>
    /// 특정 그룹에 소속된 모든 캐릭터 프리셋 리스트를 반환합니다.
    /// </summary>
    public List<CharacterPreset> GetGroupMembers(string groupID)
    {
        CharacterGroup group = GetGroup(groupID);
        if (group == null) return new List<CharacterPreset>();

        // LINQ를 사용하여 ID 목록에 해당하는 프리셋 객체들을 효율적으로 찾아서 반환
        return CharacterPresetManager.Instance.presets
            .Where(p => group.memberPresetIDs.Contains(p.presetID))
            .ToList();
    }
    
    /// <summary>
    /// 그룹 채팅방에 시스템 메시지를 기록하는 헬퍼 함수.
    /// </summary>
    private void LogSystemMessageToGroupChat(string groupID, string localizationKey, string characterName)
    {
        // 현지화된 문자열을 가져옴
        var localizedString = new LocalizedString("string Table", localizationKey);
        localizedString.Arguments = new object[] { new Dictionary<string, object> { { "CharacterName", characterName } } };
        string systemMessageText = localizedString.GetLocalizedString();
        
        // DB에 저장할 데이터 형식으로 변환
        var messageData = new MessageData { type = "system", textContent = systemMessageText };
        string messageJson = JsonUtility.ToJson(messageData);
        
        // DB에 기록
        ChatDatabaseManager.Instance.InsertGroupMessage(groupID, "system", messageJson);
    }
    
    #endregion
}

// 그룹 데이터를 담는 직렬화 가능한 클래스.
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
    
    // Key: 멤버 프리셋 ID, Value: 해당 멤버의 그룹 내 역할 설명
    public Dictionary<string, string> memberRoles = new Dictionary<string, string>();
    
    // Key: 멤버 A의 프리셋 ID, Value: [Key: 멤버 B의 프리셋 ID, Value: A가 B에 대해 가지는 관계 설명]
    public Dictionary<string, Dictionary<string, string>> memberRelationships 
            = new Dictionary<string, Dictionary<string, string>>();
    
    // 그룹의 상태 기억
    public string currentContextSummary; // 현재 상황 요약 (단기)
    public List<string> groupLongTermMemories = new List<string>(); // 그룹 장기 기억
    public Dictionary<string, string> groupKnowledgeLibrary = new Dictionary<string, string>(); // 그룹 초장기 기억
    public int lastSummarizedGroupMessageId = 0; // 그룹 대화 요약 위치 추적
}