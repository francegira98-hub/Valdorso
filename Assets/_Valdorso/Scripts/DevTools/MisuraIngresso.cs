using Mirror;
using UnityEngine;

namespace Valdorso.DevTools
{
    /// <summary>
    /// Misura l'ingresso nella valle dopo la scelta del personaggio, per capire dove va il tempo:
    /// quando il personaggio UMA è costruito, quanto dura il primo fotogramma della valle
    /// e quanti fotogrammi lenti (sopra 50 ms) ci sono nei primi 5 secondi. Scrive tutto in Console e poi sparisce.
    /// Strumento di prova: funziona solo nell'editor e nelle build di sviluppo (Development Build).
    /// </summary>
    public class MisuraIngresso : MonoBehaviour
    {
        double giaPassati;
        float tempo;
        int fotogrammi;
        int lenti;
        float peggiore;
        float primo = -1f;
        bool umaPronto;

        public static void Avvia(double millisecondiGiaPassati)
        {
            // Solo nell'editor e nelle build di sviluppo: nel gioco vero non misura e non scrive niente
            if (!Debug.isDebugBuild) return;
            var go = new GameObject("Misura ingresso");
            DontDestroyOnLoad(go);
            go.AddComponent<MisuraIngresso>().giaPassati = millisecondiGiaPassati;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            tempo += dt;
            fotogrammi++;
            if (fotogrammi == 2) primo = dt; // il primo fotogramma "vero" con la valle riaccesa
            if (dt > 0.05f) lenti++;
            if (dt > peggiore) peggiore = dt;

            if (!umaPronto && CorpoPronto())
            {
                umaPronto = true;
                Debug.Log($"[Valdorso][Ingresso] personaggio UMA costruito dopo {Totale()} ms");
            }

            if (tempo >= 5f)
            {
                Debug.Log($"[Valdorso][Ingresso] primi 5 secondi: primo fotogramma {primo * 1000f:0} ms, " +
                          $"il più lento {peggiore * 1000f:0} ms, fotogrammi lenti (oltre 50 ms) {lenti} su {fotogrammi}.");
                Destroy(gameObject);
            }
        }

        /// <summary>Il corpo UMA è costruito quando c'è una mesh con i vertici (prima c'è solo lo scheletro vuoto).</summary>
        static bool CorpoPronto()
        {
            if (NetworkClient.localPlayer == null) return false;
            var r = NetworkClient.localPlayer.GetComponentInChildren<SkinnedMeshRenderer>();
            return r != null && r.sharedMesh != null && r.sharedMesh.vertexCount > 0;
        }

        double Totale() => giaPassati + tempo * 1000.0;
    }
}