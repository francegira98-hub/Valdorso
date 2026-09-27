using Mirror;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;

namespace Valdorso.Creatures
{
    /// <summary>
    /// L'aspetto del personaggio in rete.
    /// Il server conserva la "ricetta" UMA (una stringa che descrive corpo, colori e vestiti)
    /// e la manda a tutti i PC, che costruiscono lo stesso personaggio.
    /// Se il personaggio non ha ancora una ricetta (personaggio provvisorio), il PC di chi gioca
    /// ne inventa una a caso e la manda al server, che la conserva nella scheda.
    /// Quando ci sarà la creazione del personaggio (passo 5), la ricetta arriverà da lì.
    /// </summary>
    [RequireComponent(typeof(DynamicCharacterAvatar))]
    public class UmaAppearance : NetworkBehaviour
    {
        [Tooltip("Lunghezza massima della ricetta accettata dal server (caratteri)")]
        [SerializeField] int maxRecipeLength = 8000;

        [Header("Aspetto casuale (finché non c'è la creazione del personaggio)")]
        [Tooltip("Di quanto può cambiare ogni misura del corpo rispetto a quella di partenza (0-0,3)")]
        [SerializeField, Range(0f, 0.3f)] float dnaVariation = 0.12f;

        [SerializeField]
        Color[] skinTones =
        {
            new Color(1.00f, 0.92f, 0.86f), new Color(0.96f, 0.84f, 0.74f), new Color(0.90f, 0.76f, 0.64f),
            new Color(0.80f, 0.64f, 0.50f), new Color(0.66f, 0.50f, 0.38f), new Color(0.48f, 0.34f, 0.25f)
        };

        [SerializeField]
        Color[] hairColors =
        {
            new Color(0.08f, 0.07f, 0.06f), new Color(0.20f, 0.13f, 0.08f), new Color(0.35f, 0.22f, 0.12f),
            new Color(0.50f, 0.26f, 0.12f), new Color(0.72f, 0.56f, 0.34f), new Color(0.55f, 0.52f, 0.50f)
        };

        [SerializeField]
        Color[] eyeColors =
        {
            new Color(0.30f, 0.20f, 0.12f), new Color(0.45f, 0.35f, 0.18f), new Color(0.30f, 0.45f, 0.30f),
            new Color(0.35f, 0.50f, 0.70f), new Color(0.50f, 0.55f, 0.60f)
        };

        // La ricetta decisa dal server: quando cambia, ogni PC ricostruisce il personaggio.
        [SyncVar(hook = nameof(OnRecipeSync))] string recipe = "";

        DynamicCharacterAvatar avatar;
        bool firstBuildDone;      // UMA ha finito di costruire il personaggio almeno una volta
        string pendingRecipe;     // ricetta arrivata prima che UMA fosse pronto
        string appliedRecipe;     // ultima ricetta messa su questo PC (per non ricostruire due volte)
        bool randomRequested;     // il PC di chi gioca deve inventare un aspetto
        bool actNextFrame;        // UMA ha appena finito: si agisce al fotogramma dopo, non durante il suo avviso

        /// <summary>La ricetta attuale (sul server la legge il NetworkManager per salvarla nella scheda).</summary>
        public string Recipe => recipe;

        void Awake()
        {
            avatar = GetComponent<DynamicCharacterAvatar>();
            avatar.OnCharacterCreated += OnAvatarBuilt;
            avatar.OnCharacterUpdated += OnAvatarBuilt;
        }

        void OnDestroy()
        {
            if (avatar == null) return;
            avatar.OnCharacterCreated -= OnAvatarBuilt;
            avatar.OnCharacterUpdated -= OnAvatarBuilt;
        }

        // ---------- Server ----------

        /// <summary>Il NetworkManager la chiama prima di far comparire il personaggio, con la ricetta della scheda.</summary>
        [Server]
        public void SetRecipeOnServer(string newRecipe)
        {
            recipe = newRecipe ?? "";
        }

        [Command]
        void CmdSubmitRandomRecipe(string newRecipe)
        {
            // Un aspetto casuale si accetta solo se il personaggio non ne ha ancora uno:
            // cambiarlo dopo sarà compito della creazione del personaggio, decisa dal server.
            if (!string.IsNullOrEmpty(recipe)) return;
            if (string.IsNullOrEmpty(newRecipe) || newRecipe.Length > maxRecipeLength ||
                !(newRecipe.StartsWith("BB*") || newRecipe.StartsWith("AA*")))
            {
                Debug.LogWarning($"[Valdorso] Ricetta dell'aspetto rifiutata per {name} (vuota, troppo lunga o in un formato sconosciuto).");
                return;
            }
            recipe = newRecipe;
            Debug.Log($"[Valdorso] Aspetto casuale ricevuto per {name} ({newRecipe.Length} caratteri).");
        }

        // ---------- Client ----------

        public override void OnStartClient()
        {
            Show(recipe);
        }

        public override void OnStartLocalPlayer()
        {
            if (!string.IsNullOrEmpty(recipe)) return;
            randomRequested = true;
            if (firstBuildDone) MakeRandomAppearance();
        }

        void OnRecipeSync(string oldRecipe, string newRecipe)
        {
            if (isClient) Show(newRecipe);
        }

        void OnAvatarBuilt(UMAData data)
        {
            // Qui UMA sta ancora chiudendo il suo lavoro: ricostruire adesso gli toglierebbe
            // lo scheletro da sotto i piedi (NullReferenceException in UMAReady).
            // Ci segniamo solo che è pronto e agiamo al fotogramma successivo.
            if (firstBuildDone) return;
            firstBuildDone = true;
            actNextFrame = true;
        }

        void Update()
        {
            if (!actNextFrame) return;
            actNextFrame = false;
            if (!isClient) return;

            if (!string.IsNullOrEmpty(pendingRecipe))
            {
                string r = pendingRecipe;
                pendingRecipe = null;
                Apply(r);
            }
            else if (randomRequested)
            {
                MakeRandomAppearance();
            }
        }

        void Show(string r)
        {
            if (string.IsNullOrEmpty(r) || r == appliedRecipe) return;
            if (!firstBuildDone || actNextFrame)
            {
                pendingRecipe = r;
                return;
            }
            Apply(r);
        }

        void Apply(string r)
        {
            appliedRecipe = r;
            try
            {
                avatar.LoadAvatarDefinition(r);
                avatar.BuildCharacter(true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Valdorso] Ricetta dell'aspetto non leggibile per {name}: {e.Message}");
            }
        }

        void MakeRandomAppearance()
        {
            randomRequested = false;
            if (!string.IsNullOrEmpty(recipe)) return; // nel frattempo è arrivata quella del server

            // Corpo e viso: ogni misura si sposta un poco dal valore di partenza.
            // Le misure del colore della pelle si lasciano stare (darebbero toni verdi o blu).
            foreach (DnaSetter dna in avatar.GetDNA().Values)
            {
                if (dna.Name.ToLowerInvariant().Contains("skin")) continue;
                dna.Set(Mathf.Clamp01(dna.Value + Random.Range(-dnaVariation, dnaVariation)));
            }

            SetColorIfPresent("Skin", Pick(skinTones));
            SetColorIfPresent("Hair", Pick(hairColors));
            SetColorIfPresent("Eyes", Pick(eyeColors));

            string newRecipe = avatar.GetAvatarDefinition(false, false).ToCompressedString();
            Apply(newRecipe);
            CmdSubmitRandomRecipe(newRecipe);
            Debug.Log($"[Valdorso] Inventato un aspetto casuale ({newRecipe.Length} caratteri), mandato al server.");
        }

        void SetColorIfPresent(string colorName, Color color)
        {
            OverlayColorData current = avatar.GetColor(colorName);
            if (current == null) return;
            OverlayColorData copy = current.Clone();
            color.a = 1f;
            copy.color = color;
            avatar.SetColor(colorName, copy, false);
        }

        static Color Pick(Color[] palette)
        {
            return palette != null && palette.Length > 0 ? palette[Random.Range(0, palette.Length)] : Color.white;
        }
    }
}