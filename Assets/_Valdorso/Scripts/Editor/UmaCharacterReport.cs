using System.Text;
using UMA;
using UMA.CharacterSystem;
using UnityEditor;
using UnityEngine;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Descrive il personaggio UMA selezionato (in Play): pezzi, materiali, shader, colori condivisi.
    /// Serve a capire perché un colore non arriva su una parte (per esempio i capelli).
    /// Menu di Unity: Valdorso → Prove → Descrivi il personaggio UMA selezionato.
    /// Il risultato va nella Console e negli appunti, pronto da incollare in chat.
    /// </summary>
    public static class UmaCharacterReport
    {
        [MenuItem("Valdorso/Prove/Descrivi il personaggio UMA selezionato")]
        static void Report()
        {
            GameObject selected = Selection.activeGameObject;
            DynamicCharacterAvatar avatar = selected != null ? selected.GetComponentInParent<DynamicCharacterAvatar>() : null;
            if (!EditorApplication.isPlaying || avatar == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Premi Play e seleziona un personaggio UMA (per esempio Personaggio nella scena Creazione).", "OK");
                return;
            }

            var text = new StringBuilder();
            text.AppendLine($"Personaggio: {avatar.name}, razza {avatar.activeRace.name}");

            UMAData.UMARecipe recipe = avatar.umaRecipe;
            if (recipe != null && recipe.slotDataList != null)
            {
                foreach (SlotData slot in recipe.slotDataList)
                {
                    if (slot == null) continue;
                    UMAMaterial um = slot.material;
                    string shader = um != null && um.material != null ? um.material.shader.name : "?";
                    text.AppendLine($"- Pezzo {slot.slotName}: materiale {(um != null ? um.objectName : "?")} ({(um != null ? um.materialType.ToString() : "?")}), shader {shader}");
                    if (um != null && um.shaderParms != null)
                        foreach (UMAMaterial.ShaderParms parm in um.shaderParms)
                            text.AppendLine($"    parametro {parm.ParameterName} <- colore {parm.ColorName}");
                    if (um != null && um.channels != null)
                        foreach (UMAMaterial.MaterialChannel ch in um.channels)
                            text.AppendLine($"    canale {ch.channelType} -> {ch.materialPropertyName}");
                    foreach (OverlayData overlay in slot.GetOverlayList())
                    {
                        if (overlay == null) continue;
                        string colorName = overlay.colorData != null ? overlay.colorData.name : "-";
                        Color c = overlay.colorData != null ? overlay.colorData.color : Color.white;
                        text.AppendLine($"    strato {overlay.overlayName}: colore \"{colorName}\" = {ColorUtility.ToHtmlStringRGB(c)}");
                    }
                }
            }

            text.AppendLine("Materiali sullo schermo:");
            foreach (Renderer r in avatar.GetComponentsInChildren<Renderer>())
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    string baseColor = m.HasProperty("_BaseColor") ? ColorUtility.ToHtmlStringRGB(m.GetColor("_BaseColor")) : "assente";
                    string color = m.HasProperty("_Color") ? ColorUtility.ToHtmlStringRGB(m.GetColor("_Color")) : "assente";
                    text.AppendLine($"- {r.name}: {m.name}, shader {m.shader.name}, _BaseColor {baseColor}, _Color {color}");
                }
            }

            EditorGUIUtility.systemCopyBuffer = text.ToString();
            Debug.Log("[Valdorso] Descrizione del personaggio UMA (copiata anche negli appunti):\n" + text);
        }
    }
}
