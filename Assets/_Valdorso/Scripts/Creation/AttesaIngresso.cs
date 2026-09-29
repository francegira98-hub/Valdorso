using Mirror;
using UnityEngine;
using Valdorso.UI;

namespace Valdorso.Creation
{
    /// <summary>
    /// Gli ultimi istanti dell'ingresso nella valle, ancora al buio: aspetta che il corpo UMA del personaggio sia
    /// costruito e che i primi fotogrammi della valle (quando la scheda video prepara materiali ed erba) siano passati,
    /// poi riapre il sipario. Così si entra in una valle già ferma e fluida, senza scatti né personaggi a metà.
    /// Non aspetta mai più di un paio di secondi, anche se qualcosa va storto.
    /// </summary>
    public class AttesaIngresso : MonoBehaviour
    {
        const float AttesaMassima = 2.5f;   // secondi al buio, al massimo
        const float FotogrammaFluido = 0.04f; // 40 ms: da qui in giù la valle scorre liscia
        const int FotogrammiFluidiRichiesti = 4;
        const float DurataApertura = 0.5f;

        double giaPassati;
        float tempo;
        int fluidiDiFila;
        bool corpoPronto;

        public static void Avvia(double millisecondiGiaPassati)
        {
            var go = new GameObject("Attesa ingresso");
            DontDestroyOnLoad(go);
            go.AddComponent<AttesaIngresso>().giaPassati = millisecondiGiaPassati;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            tempo += dt;

            if (!corpoPronto) corpoPronto = CorpoPronto();
            fluidiDiFila = dt <= FotogrammaFluido ? fluidiDiFila + 1 : 0;

            bool pronto = corpoPronto && fluidiDiFila >= FotogrammiFluidiRichiesti;
            if (!pronto && tempo < AttesaMassima) return;

            Debug.Log($"[Valdorso][Ingresso] sipario riaperto dopo {giaPassati + tempo * 1000.0:0} ms " +
                      (pronto ? "(valle pronta)" : "(attesa massima)"));
            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeIn(DurataApertura);
            Destroy(gameObject);
        }

        /// <summary>Il corpo UMA è costruito quando c'è una mesh con i vertici (prima c'è solo lo scheletro vuoto).</summary>
        static bool CorpoPronto()
        {
            if (NetworkClient.localPlayer == null) return false;
            var r = NetworkClient.localPlayer.GetComponentInChildren<SkinnedMeshRenderer>();
            return r != null && r.sharedMesh != null && r.sharedMesh.vertexCount > 0;
        }
    }
}
