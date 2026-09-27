using System.Collections.Generic;
using System.Linq;
using System.Text;
using UMA;
using UMA.CharacterSystem;
using UnityEditor;
using UnityEngine;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Elenca i vestiti (wardrobe recipes) di UMA che una razza può indossare, divisi per parte del corpo.
    /// Menu di Unity: Valdorso → Prove → Elenca vestiti UMA per razza.
    /// Il risultato va nella Console e negli appunti, pronto da incollare in chat.
    /// </summary>
    public static class UmaWardrobeList
    {
        static readonly string[] Races = { "Human Female 3.0", "Human Male 3.0" };

        [MenuItem("Valdorso/Prove/Elenca vestiti UMA per razza")]
        static void List()
        {
            List<UMAWardrobeRecipe> recipes = UMAAssetIndexer.Instance.GetAllAssets<UMAWardrobeRecipe>();
            var text = new StringBuilder();
            text.AppendLine($"Vestiti UMA trovati: {recipes.Count}");

            foreach (string race in Races)
            {
                text.AppendLine();
                text.AppendLine($"=== {race} ===");
                var bySlot = recipes
                    .Where(r => r != null && r.compatibleRaces != null && r.compatibleRaces.Contains(race))
                    .GroupBy(r => r.wardrobeSlot)
                    .OrderBy(g => g.Key);
                foreach (var slot in bySlot)
                    text.AppendLine($"{slot.Key}: {string.Join(", ", slot.Select(r => r.name).OrderBy(n => n))}");
            }

            EditorGUIUtility.systemCopyBuffer = text.ToString();
            Debug.Log("[Valdorso] Elenco dei vestiti UMA (copiato anche negli appunti):\n" + text);
        }
    }
}
