using DiskCardGame;
using HarmonyLib;
using Sirenix.Utilities;
using UnityEngine;

namespace InscryptionCommunityPatch.Card;

[HarmonyPatch]
internal class BoxColliderNegativeScalingLogSpamFix
{
    /// <summary>
    /// This patch fixes the incredibly annoying warning log spam of this message anytime an ability is flipped for the opponent's side.
    /// ```console
    /// [Warning: Unity Log] BoxColliders does not support negative scale or size.
    /// The effective box size has been forced positive and is likely to give unexpected collision geometry.
    /// If you absolutely need to use negative scaling you can use the convex MeshCollider.
    /// ``` 
    /// </summary>
    /// <param name="__instance">The ability icon</param>
    /// <param name="flippedY">
    /// If the icon needs to be flipped.
    /// </param>
    [HarmonyPrefix, HarmonyPatch(typeof(AbilityIconInteractable), nameof(AbilityIconInteractable.SetFlippedY))]
    private static void ReplaceBoxColliderWithMeshColliderIfIconIsFlippedY(AbilityIconInteractable __instance, bool flippedY)
    {
        if (!flippedY && !SaveManager.SaveFile.IsPart3)
            return;

        MeshCollider collider = __instance.gameObject.GetComponent<MeshCollider>();
        MeshFilter filter = __instance.gameObject.GetComponent<MeshFilter>();
        if (collider.SafeIsUnityNull() && !filter.SafeIsUnityNull())
        {
            UnityObject.Destroy(__instance.GetComponent<BoxCollider>());

            collider = __instance.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.mesh;
            collider.convex = true;

            __instance.coll = collider;
        }
    }
    [HarmonyPostfix, HarmonyPatch(typeof(AbilityIconInteractable), nameof(AbilityIconInteractable.SetFlippedY))]
    private static void OffsetFlippedColliderPositionY(AbilityIconInteractable __instance, bool flippedY)
    {
        if (!flippedY || SaveManager.SaveFile.IsPart3)
            return;

        MeshCollider collider = __instance.gameObject.GetComponent<MeshCollider>();
        if (collider.SafeIsUnityNull())
            return;

        // This was the missing piece.
        // The collider box when the MeshCollider is added ends up being right under the card, therefore unable to click.
        // Adjusting the y-position here to be higher up allows the icon to be right-clickable again.
        collider.transform.position += new Vector3(0, 0.1f, 0);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CardAbilityIcons), nameof(CardAbilityIcons.PositionModIcons))]
    private static void ReadjustFlippedMergePosition(
    List<Ability> defaultAbilities,
    List<Ability> mergeAbilities, List<AbilityIconInteractable> mergeIcons,
    List<Ability> totemAbilities, List<AbilityIconInteractable> totemIcons
    )
    {
        if (defaultAbilities.Count > 0)
            return;

        // fixes flipped merged/totem sigils being uninteractable if they're placed in the centre of the card
        if (mergeAbilities.Count == 1 && mergeIcons.Count > 0)
        {
            if (!mergeIcons[0].transform.GetComponent<MeshCollider>().SafeIsUnityNull())
                mergeIcons[0].transform.position += new Vector3(0f, 0.1f, 0f);
        }

        else if (totemAbilities.Count == 1 && totemIcons.Count > 0)
        {
            if (!totemIcons[0].transform.GetComponent<MeshCollider>().SafeIsUnityNull())
                totemIcons[0].transform.position += new Vector3(0f, 0.1f, 0f);
        }
    }
}