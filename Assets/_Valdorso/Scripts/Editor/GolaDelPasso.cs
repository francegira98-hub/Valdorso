using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Valdorso → Valle → Allarga la gola del passo: la gola dalla valle fino al bordo del mondo verso Aurelia
    /// oggi è a V, con la strada su un filo in fondo. Lo strumento le dà un fondo largo e quasi piano
    /// (24 m, con la strada che sale come prima) e fianchi raccordati più morbidi, da gola a U.
    /// Alberi, rocce e piante della natura che stanno nella zona toccata vengono rimessi sul terreno nuovo.
    /// La quota della strada si misura la prima volta e si ricorda (oggetto nascosto "Quote della gola"),
    /// così rilanciare non scava ogni volta di più.
    /// Dopo, rilanciare "Costruisci il posto di guardia" per rimettere il ripiano e i pezzi al loro posto.
    /// </summary>
    public static class GolaDelPasso
    {
        const float MezzoFondo = 12f;    // metà della larghezza del fondo piano
        const float Raccordo = 25f;      // quanto è largo il raccordo verso i fianchi
        const float InizioX = 1400f;     // da qui in poi (verso est) la gola; prima la valle, che non si tocca
        const float Dissolvenza = 40f;   // l'effetto cresce piano tra InizioX e InizioX + Dissolvenza
        const float PassoProfilo = 5f;   // la quota della strada si misura ogni 5 m

        // La strada del passo, dalla valle al bordo del mondo (da ValleMappa.Strade[0], al contrario)
        static readonly Vector2[] Strada = { new Vector2(1375, 145), new Vector2(1500, 85), new Vector2(1750, 97), new Vector2(2000, 95) };

        [MenuItem("Valdorso/Valle/Allarga la gola del passo")]
        static void Allarga()
        {
            GameObject valle = GameObject.Find("/Valle");
            if (valle == null || Terrain.activeTerrains.Length == 0)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri prima la scena Valle.", "OK");
                return;
            }

            List<Vector3> profilo = Profilo(valle.transform);
            float influenza = MezzoFondo + Raccordo;
            int campioni = 0;

            // Quote prima delle modifiche, per rimettere a posto la natura dopo
            Transform natura = valle.transform.Find("Natura");
            var daRimettere = new List<(Transform t, float prima)>();
            if (natura != null)
                foreach (Transform t in natura.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                    if (Peso(profilo, t.position.x, t.position.z, out _) <= 0f) continue;
                    daRimettere.Add((t, Altezza(t.position.x, t.position.z)));
                }

            foreach (Terrain t in Terrain.activeTerrains)
            {
                TerrainData d = t.terrainData;
                Vector3 pos = t.GetPosition(), dim = d.size;
                int res = d.heightmapResolution;
                float passo = dim.x / (res - 1);
                // Il rettangolo che contiene la gola con il raccordo
                float xMin = InizioX - influenza, xMax = 2000f + influenza, zMin = float.MaxValue, zMax = float.MinValue;
                foreach (Vector3 p in profilo) { zMin = Mathf.Min(zMin, p.z - influenza); zMax = Mathf.Max(zMax, p.z + influenza); }
                int x0 = Mathf.Clamp(Mathf.FloorToInt((xMin - pos.x) / passo), 0, res - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((xMax - pos.x) / passo), 0, res - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((zMin - pos.z) / passo), 0, res - 1);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((zMax - pos.z) / passo), 0, res - 1);
                if (x1 <= x0 || z1 <= z0) continue;

                Undo.RegisterCompleteObjectUndo(d, "Allarga la gola");
                float[,] h = d.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
                bool cambiato = false;
                for (int j = 0; j <= z1 - z0; j++)
                    for (int i = 0; i <= x1 - x0; i++)
                    {
                        float wx = pos.x + (x0 + i) * passo, wz = pos.z + (z0 + j) * passo;
                        float peso = Peso(profilo, wx, wz, out float quota);
                        if (peso <= 0f) continue;
                        h[j, i] = Mathf.Lerp(h[j, i], (quota - pos.y) / dim.y, peso);
                        cambiato = true;
                        campioni++;
                    }
                if (!cambiato) continue;
                d.SetHeights(x0, z0, h);

                // Gli alberi del terreno seguono il terreno nuovo
                TreeInstance[] alberi = d.treeInstances;
                for (int k = 0; k < alberi.Length; k++)
                {
                    Vector3 w = pos + Vector3.Scale(alberi[k].position, dim);
                    if (Peso(profilo, w.x, w.z, out _) <= 0f) continue;
                    alberi[k].position.y = t.SampleHeight(new Vector3(w.x, 0f, w.z)) / dim.y;
                }
                d.treeInstances = alberi;
            }

            // Rocce e piante della natura: stesso scarto del terreno sotto di loro
            foreach (var (t, prima) in daRimettere)
            {
                Undo.RecordObject(t, "Allarga la gola");
                t.position += Vector3.up * (Altezza(t.position.x, t.position.z) - prima);
            }

            EditorSceneManager.MarkSceneDirty(valle.scene);
            string rapporto = $"Gola allargata: fondo di {2 * MezzoFondo:0} m, raccordo di {Raccordo:0} m, {campioni} punti del terreno, " +
                              $"{daRimettere.Count} oggetti della natura rimessi sul terreno.";
            Debug.Log("[Valdorso] " + rapporto);
            EditorUtility.DisplayDialog("Valdorso", rapporto +
                "\n\nOra rilancia Costruisci il posto di guardia, poi Ctrl+S sulla scena e File → Save Project.", "OK");
        }

        /// <summary>
        /// Quanto il punto va portato alla quota del fondo (1 = del tutto, 0 = per niente) e a che quota:
        /// pieno entro MezzoFondo dalla strada, poi un raccordo morbido; all'imbocco dalla valle cresce piano.
        /// </summary>
        static float Peso(List<Vector3> profilo, float x, float z, out float quota)
        {
            quota = 0f;
            if (x < InizioX - MezzoFondo - Raccordo) return 0f;
            float dMin = float.MaxValue;
            for (int k = 0; k + 1 < profilo.Count; k++)
            {
                Vector3 a = profilo[k], b = profilo[k + 1];
                var ab = new Vector2(b.x - a.x, b.z - a.z);
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(x - a.x, z - a.z), ab) / ab.sqrMagnitude);
                var vicino = new Vector2(a.x, a.z) + ab * t;
                float dd = (new Vector2(x, z) - vicino).sqrMagnitude;
                if (dd < dMin) { dMin = dd; quota = Mathf.Lerp(a.y, b.y, t); }
            }
            float d = Mathf.Sqrt(dMin);
            if (d >= MezzoFondo + Raccordo) return 0f;
            float peso = d <= MezzoFondo ? 1f : 1f - Liscio((d - MezzoFondo) / Raccordo);
            return peso * Liscio((x - InizioX) / Dissolvenza);
        }

        /// <summary>
        /// La quota del fondo della strada ogni 5 m: il punto più basso della sezione (±8 m), poi addolcita.
        /// La prima volta si misura e si ricorda nella scena; le volte dopo si rilegge.
        /// </summary>
        static List<Vector3> Profilo(Transform valle)
        {
            Transform memoria = valle.Find("Quote della gola");
            var punti = new List<Vector3>();
            if (memoria != null && memoria.childCount > 1)
            {
                foreach (Transform c in memoria) punti.Add(c.position);
                return punti;
            }

            // Punti lungo la strada, ogni PassoProfilo metri
            var lungo = new List<Vector2>();
            for (int k = 0; k + 1 < Strada.Length; k++)
            {
                float l = Vector2.Distance(Strada[k], Strada[k + 1]);
                int n = Mathf.Max(1, Mathf.RoundToInt(l / PassoProfilo));
                for (int i = 0; i < n; i++) lungo.Add(Vector2.Lerp(Strada[k], Strada[k + 1], i / (float)n));
            }
            lungo.Add(Strada[Strada.Length - 1]);

            var quote = new float[lungo.Count];
            for (int i = 0; i < lungo.Count; i++)
            {
                Vector2 dir = i + 1 < lungo.Count ? (lungo[i + 1] - lungo[i]).normalized : (lungo[i] - lungo[i - 1]).normalized;
                var perp = new Vector2(-dir.y, dir.x);
                float minimo = float.MaxValue;
                for (float s = -8f; s <= 8f; s += 1f)
                {
                    Vector2 p = lungo[i] + perp * s;
                    minimo = Mathf.Min(minimo, Altezza(p.x, p.y));
                }
                quote[i] = minimo;
            }
            // Addolcita: media su 5 punti (25 m), così il fondo non ha gobbe
            memoria = new GameObject("Quote della gola").transform;
            memoria.SetParent(valle, false);
            memoria.gameObject.hideFlags = HideFlags.HideInHierarchy;
            Undo.RegisterCreatedObjectUndo(memoria.gameObject, "Quote della gola");
            for (int i = 0; i < lungo.Count; i++)
            {
                float somma = 0f; int n = 0;
                for (int k = Mathf.Max(0, i - 2); k <= Mathf.Min(lungo.Count - 1, i + 2); k++) { somma += quote[k]; n++; }
                var p = new Vector3(lungo[i].x, somma / n, lungo[i].y);
                var voce = new GameObject(i.ToString()).transform;
                voce.SetParent(memoria, false);
                voce.position = p;
                punti.Add(p);
            }
            return punti;
        }

        static float Altezza(float x, float z)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 pos = t.GetPosition();
                Vector3 dim = t.terrainData.size;
                if (x >= pos.x && x <= pos.x + dim.x && z >= pos.z && z <= pos.z + dim.z)
                    return pos.y + t.SampleHeight(new Vector3(x, 0f, z));
            }
            return 0f;
        }

        static float Liscio(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}