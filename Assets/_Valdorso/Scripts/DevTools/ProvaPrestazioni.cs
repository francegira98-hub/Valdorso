using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Valdorso.DevTools
{
    /// <summary>
    /// Prova delle prestazioni, per capire cosa pesa sulla scheda video. Si mette da sola, non serve aggiungerla a niente.
    /// In alto a sinistra mostra fps e millisecondi per fotogramma, e questi tasti accendono e spengono una cosa alla volta:
    /// F6 densità dell'erba 100% → 50% → 25% → 0% → 100%; F7 anti-aliasing TAA sì/no; F5 ombre del sole sì/no;
    /// F4 alberi lontani fino a 2000 m / fino a 400 m; F3 esposizione dell'immagine 0,3 → 0 → -0,3 (su una copia del profilo).
    /// Le modifiche valgono solo finché si gioca: all'uscita dal Play tutto torna com'era.
    /// Funziona solo nell'editor e nelle build di sviluppo (Development Build): nel gioco vero non esiste.
    /// </summary>
    public class ProvaPrestazioni : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Installa()
        {
            if (!Debug.isDebugBuild || Application.isBatchMode) return;
            var go = new GameObject("Prova delle prestazioni") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<ProvaPrestazioni>();
        }

        static readonly float[] Densita = { 1f, 0.5f, 0.25f, 0f };
        int densita;
        bool taa = true, ombre = true, alberiLontani = true;
        static readonly float[] Esposizioni = { 0.3f, 0f, -0.3f };
        int esposizione;
        float tempo, fotogrammi, fps, ms;
        GUIStyle stile;

        void Update()
        {
            // fps medi ogni mezzo secondo
            tempo += Time.unscaledDeltaTime;
            fotogrammi++;
            if (tempo >= 0.5f)
            {
                fps = fotogrammi / tempo;
                ms = 1000f * tempo / fotogrammi;
                tempo = 0f;
                fotogrammi = 0f;
            }

            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f6Key.wasPressedThisFrame)
            {
                densita = (densita + 1) % Densita.Length;
                foreach (Terrain t in Terrain.activeTerrains) t.detailObjectDensity = Densita[densita];
            }
            if (kb.f7Key.wasPressedThisFrame)
            {
                taa = !taa;
                Camera c = Camera.main;
                if (c != null)
                    c.GetUniversalAdditionalCameraData().antialiasing = taa ? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.None;
            }
            if (kb.f5Key.wasPressedThisFrame)
            {
                ombre = !ombre;
                if (RenderSettings.sun != null) RenderSettings.sun.shadows = ombre ? LightShadows.Soft : LightShadows.None;
            }
            if (kb.f3Key.wasPressedThisFrame)
            {
                esposizione = (esposizione + 1) % Esposizioni.Length;
                foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                {
                    if (!v.isGlobal) continue;
                    // v.profile (non sharedProfile) crea una copia solo per questa partita: il file del profilo non cambia
                    if (v.profile.TryGet(out ColorAdjustments colori)) colori.postExposure.Override(Esposizioni[esposizione]);
                }
            }
            if (kb.f4Key.wasPressedThisFrame)
            {
                alberiLontani = !alberiLontani;
                foreach (Terrain t in Terrain.activeTerrains) t.treeDistance = alberiLontani ? 2000f : 400f;
            }
        }

        void OnGUI()
        {
            if (stile == null)
            {
                stile = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft };
                stile.normal.textColor = Color.white;
            }
            string testo = $"{fps:0} fps ({ms:0.0} ms)\n" +
                           $"F6 erba {Densita[densita] * 100f:0}%   F7 TAA {(taa ? "sì" : "no")}\n" +
                           $"F5 ombre {(ombre ? "sì" : "no")}   F4 alberi fino a {(alberiLontani ? 2000 : 400)} m\n" +
                           $"F3 esposizione {Esposizioni[esposizione]:0.0}";
            GUI.Box(new Rect(10, 10, 330, 92), testo, stile);
        }
    }
}