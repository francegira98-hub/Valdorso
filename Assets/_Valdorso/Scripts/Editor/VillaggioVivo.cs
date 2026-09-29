using System.Collections.Generic;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valdorso.Interazione;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Dà vita al villaggio costruito da VillaggioBuilder, senza toccare altro:
    /// - porte: le ante SM_DoorA diventano Porta (chiusa = 90° prima di come le posa il costruttore, cioè aperte);
    /// - sedute: panche, sedie e letti diventano Sedile, con le misure della tabella qui sotto;
    /// - luoghi sicuri: una scatola invisibile SafeZone intorno alla locanda.
    /// Si può rilanciare quando si vuole: rimette le impostazioni, così una correzione fatta qui vale per tutti i pezzi uguali.
    /// Lo chiama anche il costruttore del villaggio, alla fine.
    /// </summary>
    public static class VillaggioVivo
    {
        const string PezzoPorta = "SM_DoorA";

        struct Seduta
        {
            public string pezzo, nome, azione;
            public Sedile.TipoSeduta tipo;
            public int posti;
            public float versoY, altezza;
            public Vector3 spostamento;
            public float[] versiPossibili; // se c'è: il verso si sceglie guardando intorno (tavolo davanti, muro dietro)
        }

        // Da ritoccare dopo la prima prova: versoY se ci si siede girati male, spostamento se si sta troppo avanti o indietro
        static readonly Seduta[] sedute =
        {
            new Seduta { pezzo = "SM_benchA_a1", nome = "la panca", azione = "Siediti sulla panca",
                         tipo = Sedile.TipoSeduta.Siedi, posti = 0, versoY = 90f, spostamento = new Vector3(0f, 0f, 0.25f),
                         versiPossibili = new[] { 90f, -90f } },
            new Seduta { pezzo = "SM_chairA_a1", nome = "la sedia", azione = "Siediti sulla sedia",
                         tipo = Sedile.TipoSeduta.Siedi, posti = 1, versoY = 0f, spostamento = new Vector3(0f, 0f, 0.25f),
                         versiPossibili = new[] { 0f, 90f, 180f, -90f } },
            new Seduta { pezzo = "SM_Castle_Chair_Var1", nome = "la sedia", azione = "Siediti sulla sedia",
                         tipo = Sedile.TipoSeduta.Siedi, posti = 1, versoY = 0f, spostamento = new Vector3(0f, 0f, 0.25f),
                         versiPossibili = new[] { 0f, 90f, 180f, -90f } },
            new Seduta { pezzo = "SM_Castle_Chair_Var2", nome = "la sedia", azione = "Siediti sulla sedia",
                         tipo = Sedile.TipoSeduta.Siedi, posti = 1, versoY = 0f, spostamento = new Vector3(0f, 0f, 0.25f),
                         versiPossibili = new[] { 0f, 90f, 180f, -90f } },
            new Seduta { pezzo = "SM_Castle_BED_2", nome = "il letto", azione = "Sdraiati sul letto",
                         tipo = Sedile.TipoSeduta.Sdraiati, posti = 1, versoY = 90f, altezza = 0.5f, spostamento = Vector3.zero },
        };

        // Modelli con più sedute fuse in un solo pezzo (tavolo con panche e sgabello): i posti si trovano leggendo la forma
        static readonly (string pezzo, string nome, string azione)[] tavoli =
        {
            ("SM_Tableandchairs", "il tavolo", "Siediti al tavolo"),
        };

        // I luoghi sicuri: nome dell'edificio (come lo crea il costruttore) e come si chiama nei messaggi
        static readonly (string edificio, string luogo)[] luoghiSicuri =
        {
            ("Locanda", "la Locanda"),
        };

        [MenuItem("Valdorso/Villaggio/Rendi vivo il villaggio")]
        static void DalMenu()
        {
            GameObject villaggio = GameObject.Find("/Valle/Villaggio");
            if (villaggio == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri prima la scena Valle con il villaggio costruito.", "OK");
                return;
            }
            string rapporto = Prepara(villaggio.transform);
            EditorSceneManager.MarkSceneDirty(villaggio.scene);
            Debug.Log("[Valdorso] Villaggio vivo: " + rapporto);
            EditorUtility.DisplayDialog("Valdorso", "Villaggio vivo.\n" + rapporto + "\n\nRicorda: Ctrl+S sulla scena.", "OK");
        }

        /// <summary>Prepara porte, sedute e luoghi sicuri sotto 'villaggio'. Restituisce il rapporto.</summary>
        public static string Prepara(Transform villaggio)
        {
            int porte = 0, posti = 0, luoghi = 0;
            foreach (Transform t in villaggio.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue; // i vecchi segnaposti dei tavoli vengono rifatti durante il giro
                // Solo la radice di ogni pezzo, non i suoi figli
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject) && !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                    continue;
                if (t.name.StartsWith(PezzoPorta)) { PreparaPorta(t); porte++; continue; }
                bool tavolo = false;
                foreach (var (pezzo, nome, azione) in tavoli)
                {
                    if (!t.name.StartsWith(pezzo) || t.name.Contains("_LOD")) continue;
                    posti += PreparaTavolo(t, nome, azione);
                    tavolo = true;
                    break;
                }
                if (tavolo) continue;
                foreach (Seduta s in sedute)
                {
                    if (!t.name.StartsWith(s.pezzo)) continue;
                    PreparaSeduta(t, s);
                    posti++;
                    break;
                }
            }
            foreach (var (edificio, luogo) in luoghiSicuri)
                if (PreparaLuogoSicuro(villaggio, edificio, luogo)) luoghi++;
            int bacheche = PreparaBacheche(villaggio);
            return $"{porte} porte, {posti} sedute, {luoghi} luoghi sicuri, {bacheche} bacheche.";
        }

        static void PreparaPorta(Transform t)
        {
            GameObject go = t.gameObject;
            // Un oggetto statico viene "fuso" con gli altri e non può più girare
            foreach (Transform figlio in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(figlio.gameObject, 0);

            if (go.GetComponent<Porta>() != null) return; // già pronta: gli angoli restano quelli decisi la prima volta
            if (go.GetComponent<NetworkIdentity>() == null) Undo.AddComponent<NetworkIdentity>(go);
            Porta porta = Undo.AddComponent<Porta>(go);
            float y = t.localEulerAngles.y;
            var so = new SerializedObject(porta);
            so.FindProperty("nome").stringValue = "la porta";
            so.FindProperty("angoloAperta").floatValue = y;
            so.FindProperty("angoloChiusa").floatValue = y - 90f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void PreparaSeduta(Transform t, Seduta r)
        {
            GameObject go = t.gameObject;
            if (go.GetComponent<NetworkIdentity>() == null) Undo.AddComponent<NetworkIdentity>(go);
            Sedile s = go.GetComponent<Sedile>();
            if (s == null) s = Undo.AddComponent<Sedile>(go);
            var so = new SerializedObject(s);
            so.FindProperty("nome").stringValue = r.nome;
            so.FindProperty("azione").stringValue = r.azione;
            so.FindProperty("tipo").enumValueIndex = (int)r.tipo;
            so.FindProperty("postiMassimi").intValue = r.posti;
            so.FindProperty("versoY").floatValue = r.versiPossibili != null ? ScegliVerso(t, r.versiPossibili) : r.versoY;
            so.FindProperty("altezza").floatValue = r.altezza;
            so.FindProperty("spostamento").vector3Value = r.spostamento;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Sceglie verso dove guarda chi si siede: prima di tutto verso un tavolo lì davanti; se non c'è,
        /// dalla parte con più spazio libero (così su una panca contro il muro si dà le spalle al muro).
        /// </summary>
        static float ScegliVerso(Transform t, float[] versi)
        {
            Physics.SyncTransforms();
            Bounds b = new Bounds(t.position, Vector3.zero);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            Vector3 centro = new Vector3(b.center.x, b.min.y + 0.55f, b.center.z);

            float migliore = versi[0], punteggioMigliore = float.MinValue;
            foreach (float v in versi)
            {
                Vector3 dir = t.rotation * (Quaternion.Euler(0f, v, 0f) * Vector3.forward);
                dir.y = 0f;
                dir.Normalize();
                float punteggio = 0f;
                // Quanto spazio libero c'è davanti (fino a 1,5 m), partendo dal bordo del mobile
                float fuori = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z + 0.05f; // appena fuori dal mobile
                Vector3 da = centro + dir * fuori;
                if (Physics.Raycast(da, dir, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)
                    && !hit.collider.transform.IsChildOf(t))
                {
                    string nome = NomePezzo(hit.collider.transform);
                    if (nome.Contains("table") || nome.Contains("Table")) punteggio += 10f; // un tavolo davanti: si guarda lì
                    else punteggio += hit.distance;                                            // un muro: meno spazio, peggio
                }
                else punteggio += 1.5f; // tutto libero
                if (punteggio > punteggioMigliore) { punteggioMigliore = punteggio; migliore = v; }
            }
            return migliore;
        }

        /// <summary>Il nome del pezzo del kit a cui appartiene un collider (la radice dell'istanza del prefab).</summary>
        static string NomePezzo(Transform t)
        {
            GameObject radice = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
            return radice != null ? radice.name : t.name;
        }

        /// <summary>
        /// Per i modelli con panche e sgabelli fusi al tavolo: legge la mesh più dettagliata (LOD0), trova le superfici
        /// orizzontali all'altezza di una seduta (tra 30 e 60 cm) e quelle del piano del tavolo (tra 60 cm e 1 m),
        /// raggruppa le sedute vicine (ogni gruppo è una panca o uno sgabello) e mette un posto ogni 60 cm,
        /// rivolto verso il tavolo. I posti sono oggetti vuoti figli del pezzo, sotto "Posti".
        /// Restituisce quanti posti ha messo.
        /// </summary>
        static int PreparaTavolo(Transform t, string nome, string azione)
        {
            MeshFilter mf = null;
            foreach (MeshFilter f in t.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null && (mf == null || f.name.Contains("LOD0"))) mf = f;
            if (mf == null) return 0;

            Mesh mesh = mf.sharedMesh;
            Vector3[] v = mesh.vertices;
            int[] tri = mesh.triangles;

            // Tutto nelle coordinate del pezzo (t), così non conta come è girato nel villaggio
            var punti = new Vector3[v.Length];
            float minY = float.MaxValue;
            for (int i = 0; i < v.Length; i++)
            {
                punti[i] = t.InverseTransformPoint(mf.transform.TransformPoint(v[i]));
                minY = Mathf.Min(minY, punti[i].y);
            }

            const float Cella = 0.08f;
            var celleSeduta = new HashSet<Vector2Int>();
            Vector2 centroTavolo = Vector2.zero;
            float areaTavolo = 0f;
            Vector2 tavoloMin = new Vector2(float.MaxValue, float.MaxValue), tavoloMax = new Vector2(float.MinValue, float.MinValue);
            for (int k = 0; k < tri.Length; k += 3)
            {
                Vector3 a = punti[tri[k]], b = punti[tri[k + 1]], c = punti[tri[k + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                float area = n.magnitude * 0.5f;
                if (area < 1e-6f || n.normalized.y < 0.9f) continue; // solo superfici rivolte in alto
                Vector3 centro = (a + b + c) / 3f;
                float h = centro.y - minY;
                if (h > 0.3f && h < 0.6f)
                {
                    // Segna tutte le celle coperte dal triangolo (approssimato con i suoi punti e il centro)
                    foreach (Vector3 p in new[] { a, b, c, centro, (a + b) / 2f, (b + c) / 2f, (a + c) / 2f })
                        celleSeduta.Add(new Vector2Int(Mathf.FloorToInt(p.x / Cella), Mathf.FloorToInt(p.z / Cella)));
                }
                else if (h >= 0.6f && h < 1.0f)
                {
                    centroTavolo += new Vector2(centro.x, centro.z) * area;
                    areaTavolo += area;
                    tavoloMin = Vector2.Min(tavoloMin, new Vector2(centro.x, centro.z));
                    tavoloMax = Vector2.Max(tavoloMax, new Vector2(centro.x, centro.z));
                }
            }
            if (celleSeduta.Count == 0)
            {
                Debug.LogWarning($"[Valdorso] {t.name}: nessuna superficie da seduta trovata tra 30 e 60 cm (mesh {mesh.name}, " +
                                 $"altezza totale {(t.InverseTransformPoint(mf.transform.TransformPoint(mesh.bounds.max)).y - minY):0.00} m).");
                return 0;
            }
            centroTavolo = areaTavolo > 0f ? centroTavolo / areaTavolo : Vector2.zero;

            // Raggruppa le celle vicine: ogni gruppo è una panca o uno sgabello
            var gruppi = new List<List<Vector2Int>>();
            var visti = new HashSet<Vector2Int>();
            foreach (Vector2Int inizio in celleSeduta)
            {
                if (visti.Contains(inizio)) continue;
                var gruppo = new List<Vector2Int>();
                var coda = new Queue<Vector2Int>();
                coda.Enqueue(inizio);
                visti.Add(inizio);
                while (coda.Count > 0)
                {
                    Vector2Int q = coda.Dequeue();
                    gruppo.Add(q);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            var r = new Vector2Int(q.x + dx, q.y + dz);
                            if (celleSeduta.Contains(r) && visti.Add(r)) coda.Enqueue(r);
                        }
                }
                if (gruppo.Count * Cella * Cella >= 0.04f) gruppi.Add(gruppo); // almeno 20 × 20 cm
            }

            // I posti: per ogni gruppo, lungo il lato lungo, uno ogni 60 cm, con i piedi a terra e lo sguardo verso il tavolo
            Transform contenitore = t.Find("Posti");
            if (contenitore != null) Undo.DestroyObjectImmediate(contenitore.gameObject);
            var go = new GameObject("Posti");
            Undo.RegisterCreatedObjectUndo(go, "Posti");
            contenitore = go.transform;
            contenitore.SetParent(t, false);

            var marcatori = new List<Transform>();
            foreach (List<Vector2Int> g in gruppi)
            {
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                foreach (Vector2Int q in g)
                {
                    x0 = Mathf.Min(x0, q.x * Cella); x1 = Mathf.Max(x1, (q.x + 1) * Cella);
                    z0 = Mathf.Min(z0, q.y * Cella); z1 = Mathf.Max(z1, (q.y + 1) * Cella);
                }
                // Una seduta vera è larga almeno 18 cm: così si scartano le traverse tra le gambe di tavolo e panche
                if (Mathf.Min(x1 - x0, z1 - z0) < 0.18f) continue;
                // Sotto il piano del tavolo non ci si siede: lì ci sono traverse e ripiani
                Vector2 c0 = new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f);
                if (areaTavolo > 0f && c0.x > tavoloMin.x + 0.1f && c0.x < tavoloMax.x - 0.1f
                    && c0.y > tavoloMin.y + 0.1f && c0.y < tavoloMax.y - 0.1f) continue;
                bool lungoX = (x1 - x0) >= (z1 - z0);
                float lunghezza = lungoX ? x1 - x0 : z1 - z0;
                int quanti = Mathf.Max(1, Mathf.FloorToInt(lunghezza / 0.6f));
                Vector2 centro = new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f);

                // Si guarda verso il tavolo, lungo l'asse corto della seduta
                Vector2 verso = centroTavolo - centro;
                Vector3 sguardo = lungoX ? new Vector3(0f, 0f, Mathf.Sign(verso.y)) : new Vector3(Mathf.Sign(verso.x), 0f, 0f);
                if (Mathf.Abs(x1 - x0 - (z1 - z0)) < 0.1f) // quasi quadrato (sgabello): verso l'asse più vicino al tavolo
                    sguardo = Mathf.Abs(verso.x) > Mathf.Abs(verso.y) ? new Vector3(Mathf.Sign(verso.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(verso.y));

                for (int i = 0; i < quanti; i++)
                {
                    float lungo = (i + 0.5f) / quanti * lunghezza - lunghezza / 2f;
                    Vector3 p = new Vector3(centro.x + (lungoX ? lungo : 0f), minY, centro.y + (lungoX ? 0f : lungo));
                    var m = new GameObject("Posto " + (marcatori.Count + 1)).transform;
                    m.SetParent(contenitore, false);
                    m.localPosition = p;
                    m.localRotation = Quaternion.LookRotation(sguardo, Vector3.up);
                    marcatori.Add(m);
                }
            }

            Debug.Log($"[Valdorso] {t.name}: {marcatori.Count} posti su {gruppi.Count} superfici all'altezza di una seduta.");
            GameObject radice = t.gameObject;
            if (radice.GetComponent<NetworkIdentity>() == null) Undo.AddComponent<NetworkIdentity>(radice);
            Sedile s = radice.GetComponent<Sedile>();
            if (s == null) s = Undo.AddComponent<Sedile>(radice);
            var so = new SerializedObject(s);
            so.FindProperty("nome").stringValue = nome;
            so.FindProperty("azione").stringValue = azione;
            so.FindProperty("tipo").enumValueIndex = (int)Sedile.TipoSeduta.Siedi;
            so.FindProperty("spostamento").vector3Value = new Vector3(0f, 0f, 0.25f); // come le panche: il bacino sulla seduta
            SerializedProperty lista = so.FindProperty("postiManuali");
            lista.arraySize = marcatori.Count;
            for (int i = 0; i < marcatori.Count; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = marcatori[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return marcatori.Count;
        }

        // ---------------------------------------------------------------- bacheche

        const string CartellaHivemind = "Assets/HIVEMIND";
        static readonly string[] Assi = { "SM_plankA2_a1", "SM_plankA4_a1", "SM_plankA5_a1", "SM_plankA7_a1", "SM_plankA9_a1", "SM_plankA10_a1", "SM_plankA15_a1" };

        /// <summary>
        /// Le due bacheche del villaggio, costruite con le assi del kit di Hivemind:
        /// - dentro la Gilda, grande, sul muro di fondo proprio di fronte alla porta, nel tratto di muro pieno
        ///   tra x 6 e 9 (sul retro le finestre stanno da x 3 a 6 e da 9 a 12): gli incarichi;
        /// - davanti alla casa del Balivo, all'aperto, su due pali con un tettuccio: i proclami.
        /// Ogni volta si rifanno da capo (così una correzione qui vale subito).
        /// </summary>
        static int PreparaBacheche(Transform villaggio)
        {
            int n = 0;
            Transform gilda = villaggio.Find("Gilda degli avventurieri");
            if (gilda != null && CostruisciBacheca(gilda, "Bacheca degli incarichi", new Vector3(7.4f, 0f, -5.84f), 0f, false,
                    BachecaAvvisi.TipoBacheca.Incarichi, "Gilda", "Bacheca della Gilda", "la bacheca degli incarichi")) n++;
            Transform balivo = villaggio.Find("Casa del Balivo");
            if (balivo != null && CostruisciBacheca(balivo, "Bacheca del Balivo", new Vector3(2.5f, 0f, 2.2f), 0f, true,
                    BachecaAvvisi.TipoBacheca.Proclami, "Balivo", "Proclami del Balivo", "la bacheca del Balivo")) n++;
            return n;
        }

        static bool CostruisciBacheca(Transform edificio, string nomeOggetto, Vector3 posizione, float rotY, bool allAperto,
            BachecaAvvisi.TipoBacheca tipo, string archivio, string titolo, string nome)
        {
            Transform vecchia = edificio.Find(nomeOggetto);
            if (vecchia != null) Undo.DestroyObjectImmediate(vecchia.gameObject);

            var radice = new GameObject(nomeOggetto).transform;
            Undo.RegisterCreatedObjectUndo(radice.gameObject, nomeOggetto);
            radice.SetParent(edificio, false);
            radice.localPosition = posizione;
            radice.localRotation = Quaternion.Euler(0f, rotY, 0f);

            // Il pannello: sei assi in piedi, una sopra l'altra (l'asse ruotata di 90° attorno a X diventa "in piedi")
            float zPannello = allAperto ? 0.14f : 0.05f;  // all'aperto sta davanti ai pali; al chiuso contro il muro
            const float basso = 0.95f, passo = 0.2f;
            int quante = 6;
            for (int i = 0; i < quante; i++)
                Pezzo(Assi[i % Assi.Length], radice, new Vector3(0f, basso + passo * (i + 0.5f), zPannello), new Vector3(90f, 0f, 0f));
            float alto = basso + passo * quante;

            if (allAperto)
            {
                // Due pali piantati a terra e un tettuccio spiovente all'indietro, per la pioggia
                Pezzo("SM_woodenPlankA_a1", radice, new Vector3(-0.9f, 1.3f, 0f), Vector3.zero);
                Pezzo("SM_woodenPlankA_a1", radice, new Vector3(0.9f, 1.3f, 0f), Vector3.zero);
                const float pendenza = 24f;
                float t = Mathf.Tan(pendenza * Mathf.Deg2Rad);
                for (int i = 0; i < 4; i++)
                {
                    float z = 0.45f - i * 0.2f;
                    Pezzo(Assi[(i + 2) % Assi.Length], radice, new Vector3(0f, 2.62f + (z - 0.45f) * t, z), new Vector3(-pendenza, 0f, 0f));
                }
            }
            else
            {
                // Al muro: due listelli verticali ai lati e un'asse sopra, come una cornice
                Pezzo("SM_plankB2_a1", radice, new Vector3(-0.88f, (basso + alto) / 2f, zPannello + 0.06f), new Vector3(90f, 0f, 0f));
                Pezzo("SM_plankB6_a1", radice, new Vector3(0.88f, (basso + alto) / 2f, zPannello + 0.06f), new Vector3(90f, 0f, 0f));
            }
            // L'asse di testa, orizzontale, sopra il pannello
            Pezzo("SM_plankA3_a1", radice, new Vector3(0f, alto + 0.06f, zPannello + 0.05f), Vector3.zero);

            // Rete e comportamento
            if (radice.GetComponent<NetworkIdentity>() == null) Undo.AddComponent<NetworkIdentity>(radice.gameObject);
            BachecaAvvisi b = Undo.AddComponent<BachecaAvvisi>(radice.gameObject);
            var so = new SerializedObject(b);
            so.FindProperty("nome").stringValue = nome;
            so.FindProperty("distanza").floatValue = 2.8f;
            so.FindProperty("tipo").enumValueIndex = (int)tipo;
            so.FindProperty("archivio").stringValue = archivio;
            so.FindProperty("titoloBacheca").stringValue = titolo;
            so.FindProperty("centroPannello").vector3Value = new Vector3(0f, (basso + alto) / 2f, zPannello + 0.065f);
            so.FindProperty("misuraPannello").vector2Value = new Vector2(1.55f, alto - basso - 0.05f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>Posa un pezzo del kit con il centro della sua forma nel punto dato (in coordinate del padre).</summary>
        static GameObject Pezzo(string nomePrefab, Transform padre, Vector3 centroLocale, Vector3 rotazioneLocale)
        {
            GameObject prefab = CaricaPrefab(nomePrefab);
            if (prefab == null)
            {
                Debug.LogWarning("[Valdorso] Pezzo non trovato per la bacheca: " + nomePrefab);
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(rotazioneLocale);
            bool primo = true;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (primo) { b = r.bounds; primo = false; }
                else b.Encapsulate(r.bounds);
            }
            go.transform.position += padre.TransformPoint(centroLocale) - b.center;
            return go;
        }

        static readonly Dictionary<string, GameObject> cachePrefab = new Dictionary<string, GameObject>();

        static GameObject CaricaPrefab(string nome)
        {
            if (cachePrefab.TryGetValue(nome, out GameObject p) && p != null) return p;
            foreach (string guid in AssetDatabase.FindAssets(nome + " t:Prefab", new[] { CartellaHivemind }))
            {
                string percorso = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(percorso) != nome) continue;
                p = AssetDatabase.LoadAssetAtPath<GameObject>(percorso);
                cachePrefab[nome] = p;
                return p;
            }
            return null;
        }

        /// <summary>
        /// Una scatola invisibile che copre tutto l'edificio (tutti i piani), figlia dell'edificio così ne segue la rotazione.
        /// Negli edifici del costruttore la facciata sta a z = 0, il fondo a z = -6, la lunghezza va da x = 0 a moduli × 6.
        /// </summary>
        static bool PreparaLuogoSicuro(Transform villaggio, string nomeEdificio, string luogo)
        {
            Transform edificio = villaggio.Find(nomeEdificio);
            if (edificio == null)
            {
                Debug.LogWarning("[Valdorso] Edificio non trovato per il luogo sicuro: " + nomeEdificio);
                return false;
            }
            int moduli = 1, piani = 1;
            foreach (PiantaVillaggio.Edificio e in PiantaVillaggio.Edifici)
                if (e.nome == nomeEdificio) { moduli = e.moduli; piani = e.piani; }

            Transform zona = edificio.Find("Luogo sicuro");
            if (zona == null)
            {
                var go = new GameObject("Luogo sicuro");
                Undo.RegisterCreatedObjectUndo(go, "Luogo sicuro");
                zona = go.transform;
                zona.SetParent(edificio, false);
            }
            zona.localPosition = Vector3.zero;
            zona.localRotation = Quaternion.identity;
            zona.gameObject.layer = 2; // Ignore Raycast: non ferma sguardi, telecamera e scavalcate

            BoxCollider box = zona.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(zona.gameObject);
            float lunghezza = moduli * 6f, altezza = piani * 2.5f + 2.5f; // piani da 2,5 m più il tetto
            box.isTrigger = true;
            box.center = new Vector3(lunghezza / 2f, altezza / 2f, -3f);
            box.size = new Vector3(lunghezza + 0.6f, altezza, 6.6f);

            SafeZone sz = zona.GetComponent<SafeZone>();
            if (sz == null) sz = Undo.AddComponent<SafeZone>(zona.gameObject);
            var so = new SerializedObject(sz);
            so.FindProperty("placeName").stringValue = luogo;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}