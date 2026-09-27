using UMA;
using UMA.CharacterSystem;
using UnityEngine;

namespace Valdorso.Creatures
{
    /// <summary>
    /// Fa arrivare i colori giusti su capelli, barba e sopracciglia di UMA.
    /// Con URP, UMA scrive il colore "Hair" nella proprietà _Color del materiale dei capelli,
    /// ma lo shader dei capelli (UMA3_HairShader_URP) usa _BaseColor: i capelli restavano grigio chiaro.
    /// Dopo ogni costruzione del personaggio questo componente scrive in _BaseColor:
    /// - capelli e ciglia: il colore "Hair";
    /// - barba e sopracciglia (UMA le unisce in un solo materiale, "SinglePass"): il colore "Beard"
    ///   se il personaggio ha la barba, altrimenti "Hair".
    /// Non tocca gli script di UMA. Va sulla radice del personaggio, accanto al Dynamic Character Avatar.
    /// </summary>
    [RequireComponent(typeof(DynamicCharacterAvatar))]
    public class UmaHairTint : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        DynamicCharacterAvatar avatar;

        void Awake()
        {
            avatar = GetComponent<DynamicCharacterAvatar>();
            avatar.OnCharacterCreated += OnBuilt;
            avatar.OnCharacterUpdated += OnBuilt;
        }

        void OnDestroy()
        {
            if (avatar == null) return;
            avatar.OnCharacterCreated -= OnBuilt;
            avatar.OnCharacterUpdated -= OnBuilt;
        }

        void Start() => Apply();

        // Qui si cambiano solo i colori dei materiali, non si ricostruisce il personaggio: si può fare subito.
        void OnBuilt(UMAData data) => Apply();

        public void Apply()
        {
            if (avatar == null) return;
            Color? hair = ColorOf("Hair");
            Color? whiskers = HasBeard() ? ColorOf("Beard") ?? hair : hair;

            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || !m.HasProperty(BaseColorId)) continue;
                    string n = m.name;
                    Color? c = n.Contains("SinglePass") ? whiskers
                             : n.Contains("Hair") || n.Contains("Eyelashes") ? hair
                             : null;
                    if (c == null) continue;
                    Color value = c.Value;
                    value.a = 1f;
                    m.SetColor(BaseColorId, value);
                }
            }
        }

        Color? ColorOf(string name)
        {
            OverlayColorData data = avatar.GetColor(name);
            return data != null ? data.color : (Color?)null;
        }

        bool HasBeard()
        {
            UMAData.UMARecipe recipe = avatar.umaRecipe;
            if (recipe == null || recipe.slotDataList == null) return false;
            foreach (SlotData slot in recipe.slotDataList)
            {
                if (slot == null) continue;
                string n = slot.slotName;
                if (n.StartsWith("Beard") || n.StartsWith("Mustache")) return true;
            }
            return false;
        }
    }
}