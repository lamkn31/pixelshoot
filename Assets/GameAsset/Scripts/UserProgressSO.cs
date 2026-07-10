using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Bus Game/UserProgress", fileName = "UserProgress")]
public sealed class UserProgressSO : ScriptableObject
{
    public int currentLevelIndex = 0;

    [Header("Booster inventory (player-owned, decremented per use, persisted)")]
    [Tooltip("Add-slot booster uses the player owns globally (the 'y' in the x/y counter). " +
             "Each add-slot use decrements this; the per-level config (slotAddCount) is the 'x'.")]
    public int addSlotCount = 0;
    [Tooltip("Remove-stickman booster uses the player owns globally. Each use decrements this. " +
             "Not stored per-level. (Revive triggers the booster for FREE — does not consume this.)")]
    public int removeStickmanCount = 0;

    [System.Serializable]
    public struct LoseEntry { public string levelId; public int count; }

    public List<LoseEntry> loseCounts = new List<LoseEntry>();

    private const string PrefKeyIndex = "BusFever.Progress.CurrentLevel";
    private const string PrefKeyLosses = "BusFever.Progress.Losses";
    private const string PrefKeyAddSlot = "BusFever.Progress.AddSlotCount";
    private const string PrefKeyRemove = "BusFever.Progress.RemoveStickmanCount";

    /// <summary>Spend one add-slot booster from the player's inventory. Returns false (no-op) when
    /// the inventory is already empty.</summary>
    public bool ConsumeAddSlot()
    {
        if (addSlotCount <= 0) return false;
        addSlotCount--;
        Save();
        return true;
    }

    /// <summary>Spend one remove-stickman booster from the player's inventory. Returns false (no-op)
    /// when the inventory is already empty.</summary>
    public bool ConsumeRemoveStickman()
    {
        if (removeStickmanCount <= 0) return false;
        removeStickmanCount--;
        Save();
        return true;
    }

    public int GetLoseCount(string levelId)
    {
        for (int i = 0; i < loseCounts.Count; i++)
            if (loseCounts[i].levelId == levelId) return loseCounts[i].count;
        return 0;
    }

    public void IncrementLose(string levelId)
    {
        for (int i = 0; i < loseCounts.Count; i++)
        {
            if (loseCounts[i].levelId == levelId)
            {
                var e = loseCounts[i];
                e.count++;
                loseCounts[i] = e;
                Save();
                return;
            }
        }
        loseCounts.Add(new LoseEntry { levelId = levelId, count = 1 });
        Save();
    }

    public void AdvanceLevel()
    {
        currentLevelIndex++;
        Save();
    }

    public void Load()
    {
        currentLevelIndex = PlayerPrefs.GetInt(PrefKeyIndex, 0);
        addSlotCount = PlayerPrefs.GetInt(PrefKeyAddSlot, addSlotCount);
        removeStickmanCount = PlayerPrefs.GetInt(PrefKeyRemove, removeStickmanCount);
        string json = PlayerPrefs.GetString(PrefKeyLosses, "");
        if (!string.IsNullOrEmpty(json))
        {
            var wrap = JsonUtility.FromJson<LoseWrap>(json);
            if (wrap != null && wrap.items != null) loseCounts = new List<LoseEntry>(wrap.items);
        }
    }

    public void Save()
    {
        PlayerPrefs.SetInt(PrefKeyIndex, currentLevelIndex);
        PlayerPrefs.SetInt(PrefKeyAddSlot, addSlotCount);
        PlayerPrefs.SetInt(PrefKeyRemove, removeStickmanCount);
        var wrap = new LoseWrap { items = loseCounts.ToArray() };
        PlayerPrefs.SetString(PrefKeyLosses, JsonUtility.ToJson(wrap));
        PlayerPrefs.Save();
    }

    [System.Serializable]
    private class LoseWrap { public LoseEntry[] items; }
}
