
using UnityEngine;

public static class CharacterSelectionState
{
    public static string SelectedCharacterId { get; private set; }
    public static bool HasSelection => !string.IsNullOrWhiteSpace(SelectedCharacterId);

    public static void SetSelectedCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return;

        SelectedCharacterId = characterId;
    }

    public static void CaptureFromPlayer(GameObject player)
    {
        if (player == null)
            return;

        PlayerCharacterId characterId = player.GetComponent<PlayerCharacterId>();
        if (characterId == null)
            characterId = player.GetComponentInChildren<PlayerCharacterId>();

        if (characterId != null)
            SetSelectedCharacter(characterId.characterId);
    }

    public static void Clear()
    {
        SelectedCharacterId = null;
    }
}
