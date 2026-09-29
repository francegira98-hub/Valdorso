using System.Collections.Generic;
using System.Linq;
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
    /// - luoghi sicuri: una scatola invisibile SafeZone intorno alla locanda;
    /// - arredi: i mobili dentro gli edifici vanno sul livello "Arredi" (lo crea se manca), che "scavalca e sali" ignora.
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
            int finestre = PreparaImposte(villaggio);
            int arredi = PreparaArredi(villaggio);
            return $"{porte} porte, {posti} sedute, {luoghi} luoghi sicuri, {bacheche} bacheche, {finestre} finestre con le imposte, {arredi} arredi.";
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

        // ---------------------------------------------------------------- imposte delle finestre

        static readonly string[] Ante = { "SM_WindowA_a1", "SM_WindowB_a1", "SM_WindowC_a1", "SM_WindowD_a1" };
        const float LarghezzaAnta = 0.585f, AltezzaAnta = 1.03f; // misurate dal kit: perno sul cardine, l'anta va verso -X

        /// <summary>
        /// Monta due imposte (una normale e una specchiata) su ogni vano delle finestre del villaggio.
        /// Il vano non si indovina: si "tasta" il muro con tanti piccoli raggi e si trova il buco.
        /// Le imposte stanno sul lato esterno del muro, si aprono verso fuori fino ad appoggiarsi al muro.
        /// Per ogni finestra c'è un solo oggetto "Imposte" con NetworkIdentity e Finestra: E apre e chiude tutte e due.
        /// </summary>
        static int PreparaImposte(Transform villaggio)
        {
            int n = 0, edificio = 0, muri = 0, senzaVano = 0;
            foreach (Transform casa in villaggio)
            {
                edificio++;
                // Il centro della casa, per sapere da che parte è "fuori" per ogni muro
                Bounds forma = new Bounds(casa.position, Vector3.zero);
                foreach (Renderer r in casa.GetComponentsInChildren<Renderer>()) forma.Encapsulate(r.bounds);

                foreach (Transform muro in casa)
                {
                    if (!muro.name.StartsWith("SM_wallWindow") || muro.name.Contains("Opened")) continue;
                    // Rifà da capo le imposte di questo muro
                    for (int i = muro.childCount - 1; i >= 0; i--)
                        if (muro.GetChild(i).name.StartsWith("Imposte")) Undo.DestroyObjectImmediate(muro.GetChild(i).gameObject);

                    muri++;
                    List<Rect> vani = TrovaVani(muro);
                    if (vani.Count == 0) senzaVano++;
                    foreach (Rect vano in vani)
                        if (MontaImposte(muro, vano, forma.center, Ante[edificio % Ante.Length])) n++;
                }
            }
            Debug.Log($"[Valdorso] Imposte: {muri} muri con finestra, {n} vani trovati e montati, {senzaVano} muri dove il vano non si trova.");
            return n;
        }

        /// <summary>
        /// I vani (buchi) di un muro, nelle coordinate del muro: X lungo il muro, Y in altezza.
        /// Si guarda la forma disegnata del muro (le sue mesh, non il collider, che spesso è una scatola piena):
        /// si "proietta" ogni triangolo sul piano del muro e si segna la griglia coperta; ciò che resta scoperto è il vano.
        /// </summary>
        static List<Rect> TrovaVani(Transform muro)
        {
            var vani = new List<Rect>();

            // Le mesh del muro: solo quelle più dettagliate (LOD0), se il pezzo ha i livelli di dettaglio
            var mesh = new List<MeshFilter>();
            foreach (MeshFilter mf in muro.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.name.Contains("LOD0")) mesh.Add(mf);
            if (mesh.Count == 0)
                foreach (MeshFilter mf in muro.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null) mesh.Add(mf);
            if (mesh.Count == 0) return vani;

            // I triangoli nelle coordinate del muro, e quanto è lungo il muro
            var triangoli = new List<Vector3>();
            float x0 = float.MaxValue, x1 = float.MinValue;
            foreach (MeshFilter mf in mesh)
            {
                Vector3[] v = mf.sharedMesh.vertices;
                int[] t = mf.sharedMesh.triangles;
                var locali = new Vector3[v.Length];
                for (int k = 0; k < v.Length; k++)
                {
                    locali[k] = muro.InverseTransformPoint(mf.transform.TransformPoint(v[k]));
                    x0 = Mathf.Min(x0, locali[k].x);
                    x1 = Mathf.Max(x1, locali[k].x);
                }
                for (int k = 0; k < t.Length; k++) triangoli.Add(locali[t[k]]);
            }
            if (x1 <= x0) return vani;

            const float Passo = 0.05f;
            int nx = Mathf.CeilToInt((x1 - x0) / Passo), ny = Mathf.CeilToInt(2.4f / Passo);
            var pieno = new bool[nx, ny];
            for (int k = 0; k < triangoli.Count; k += 3)
            {
                Vector2 a = new Vector2(triangoli[k].x, triangoli[k].y);
                Vector2 b = new Vector2(triangoli[k + 1].x, triangoli[k + 1].y);
                Vector2 c = new Vector2(triangoli[k + 2].x, triangoli[k + 2].y);
                float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                if (Mathf.Abs(area) < 1e-5f) continue; // triangolo "di taglio" (lo spessore del muro): non copre niente
                int ix0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x, c.x) - x0) / Passo));
                int ix1 = Mathf.Min(nx - 1, Mathf.FloorToInt((Mathf.Max(a.x, b.x, c.x) - x0) / Passo));
                int iy0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y) / Passo));
                int iy1 = Mathf.Min(ny - 1, Mathf.FloorToInt(Mathf.Max(a.y, b.y, c.y) / Passo));
                for (int ix = ix0; ix <= ix1; ix++)
                    for (int iy = iy0; iy <= iy1; iy++)
                    {
                        if (pieno[ix, iy]) continue;
                        var p = new Vector2(x0 + (ix + 0.5f) * Passo, (iy + 0.5f) * Passo);
                        if (DentroTriangolo(p, a, b, c)) pieno[ix, iy] = true;
                    }
            }

            var buco = new bool[nx, ny];
            for (int ix = 1; ix < nx - 1; ix++)
                for (int iy = 4; iy < ny; iy++) // da 20 cm in su
                    buco[ix, iy] = !pieno[ix, iy];

            // Raggruppa i punti "vuoti" vicini: ogni gruppo abbastanza grande è un vano
            var visti = new bool[nx, ny];
            for (int ix = 0; ix < nx; ix++)
                for (int iy = 0; iy < ny; iy++)
                {
                    if (!buco[ix, iy] || visti[ix, iy]) continue;
                    int a0 = ix, a1 = ix, b0 = iy, b1 = iy, celle = 0;
                    var coda = new Queue<Vector2Int>();
                    coda.Enqueue(new Vector2Int(ix, iy));
                    visti[ix, iy] = true;
                    while (coda.Count > 0)
                    {
                        Vector2Int q = coda.Dequeue();
                        celle++;
                        a0 = Mathf.Min(a0, q.x); a1 = Mathf.Max(a1, q.x); b0 = Mathf.Min(b0, q.y); b1 = Mathf.Max(b1, q.y);
                        foreach (Vector2Int d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                        {
                            Vector2Int r = q + d;
                            if (r.x < 0 || r.y < 0 || r.x >= nx || r.y >= ny || visti[r.x, r.y] || !buco[r.x, r.y]) continue;
                            visti[r.x, r.y] = true;
                            coda.Enqueue(r);
                        }
                    }
                    float w = (a1 - a0 + 1) * Passo, h = (b1 - b0 + 1) * Passo;
                    if (w < 0.4f || h < 0.4f || w > 2.2f) continue; // troppo piccolo, o non è una finestra
                    vani.Add(new Rect(x0 + a0 * Passo, b0 * Passo, w, h));
                }
            return vani;
        }

        static bool DentroTriangolo(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static bool MontaImposte(Transform muro, Rect vano, Vector3 centroCasa, string pezzo)
        {
            GameObject prefab = CaricaPrefab(pezzo);
            if (prefab == null) { Debug.LogWarning("[Valdorso] Anta non trovata: " + pezzo); return false; }

            // Da che parte è fuori: l'avanti del muro punta verso fuori oppure verso dentro?
            float verso = Vector3.Dot(muro.forward, muro.position - centroCasa) >= 0f ? 1f : -1f;
            const float Incasso = 0.14f; // dentro lo spessore del muro, verso fuori: le ante stanno nel riquadro del vano

            var contenitore = new GameObject("Imposte").transform;
            Undo.RegisterCreatedObjectUndo(contenitore.gameObject, "Imposte");
            contenitore.SetParent(muro, false);
            contenitore.localPosition = new Vector3(vano.center.x, vano.yMin, verso * Incasso);
            contenitore.localRotation = Quaternion.identity;

            float scalaX = vano.width / 2f / LarghezzaAnta, scalaY = vano.height / AltezzaAnta;

            // Anta destra: cardine sul bordo destro del vano, l'anta va verso sinistra (-X), come nel kit
            Transform destra = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, contenitore)).transform;
            destra.name = "Anta destra";
            destra.localPosition = new Vector3(vano.width / 2f, 0f, 0f);
            destra.localScale = new Vector3(scalaX, scalaY, 1f);

            // Anta sinistra: la stessa, specchiata, con il cardine sul bordo sinistro
            Transform sinistra = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, contenitore)).transform;
            sinistra.name = "Anta sinistra";
            sinistra.localPosition = new Vector3(-vano.width / 2f, 0f, 0f);
            sinistra.localScale = new Vector3(-scalaX, scalaY, 1f);

            foreach (Transform t in new[] { destra, sinistra })
                foreach (Transform f in t.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(f.gameObject, 0); // girano: niente Static

            // Aperte: girate verso fuori di 100°, una da una parte e una dall'altra (come ante vere dentro il loro telaio)
            float apertaDestra = 100f * verso, apertaSinistra = -100f * verso;

            // Il punto da guardare: il centro del vano, a metà del muro
            var centro = new GameObject("Centro").transform;
            centro.SetParent(contenitore, false);
            centro.localPosition = new Vector3(0f, vano.height / 2f, -verso * Incasso);

            Undo.AddComponent<NetworkIdentity>(contenitore.gameObject);
            Finestra finestra = Undo.AddComponent<Finestra>(contenitore.gameObject);
            var so = new SerializedObject(finestra);
            so.FindProperty("nome").stringValue = "le imposte";
            so.FindProperty("distanza").floatValue = 2.4f;
            so.FindProperty("punto").objectReferenceValue = centro;
            SerializedProperty ante = so.FindProperty("ante");
            ante.arraySize = 2;
            ante.GetArrayElementAtIndex(0).FindPropertyRelative("perno").objectReferenceValue = destra;
            ante.GetArrayElementAtIndex(0).FindPropertyRelative("chiusaY").floatValue = 0f;
            ante.GetArrayElementAtIndex(0).FindPropertyRelative("apertaY").floatValue = apertaDestra;
            ante.GetArrayElementAtIndex(1).FindPropertyRelative("perno").objectReferenceValue = sinistra;
            ante.GetArrayElementAtIndex(1).FindPropertyRelative("chiusaY").floatValue = 0f;
            ante.GetArrayElementAtIndex(1).FindPropertyRelative("apertaY").floatValue = apertaSinistra;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
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
        // ---------------------------------------------------------------- Arredi

        // I pezzi che fanno la casa (non sono arredi): pavimenti, muri, tetti, camini, angoli, porte
        static readonly string[] Struttura = { "SM_floor", "SM_wall", "SM_roof", "SM_chimney", "SM_planksCorner", "SM_Door" };

        /// <summary>
        /// Mette sul livello "Arredi" tutto ciò che sta dentro gli edifici e non è la casa stessa
        /// (tavoli, sedie, letti, bauli, scaffali, botti, camini, candele...). Fuori non cambia niente:
        /// casse, botti, carri, staccionate e panche accanto alle porte restano da scavalcare.
        /// Tocca solo gli oggetti sul livello Default (il luogo sicuro resta su Ignore Raycast).
        /// </summary>
        static int PreparaArredi(Transform villaggio)
        {
            // Il livello 8 è quello del giocatore (Giocatore_UMA, dagli Starter Assets): non va mai usato per i mobili,
            // altrimenti il controller conta il giocatore stesso come terreno e salta all'infinito.
            // Se una versione precedente di questo strumento l'aveva chiamato "Arredi", lo si rinomina "Personaggi".
            RinominaLivello(8, ObstacleTraversalArredi, "Personaggi");
            int livello = AssicuraLivello(ObstacleTraversalArredi, 10);
            if (livello < 0)
            {
                Debug.LogWarning("[Valdorso] Non c'è un livello libero per gli Arredi (Tags and Layers).");
                return 0;
            }

            int n = 0;
            foreach (Transform edificio in villaggio)
            {
                // Gli edifici sono oggetti vuoti creati dal costruttore; i pezzi sparsi fuori sono prefab
                if (PrefabUtility.IsPartOfPrefabInstance(edificio.gameObject)) continue;
                foreach (Transform pezzo in edificio)
                {
                    if (Struttura.Any(s => pezzo.name.StartsWith(s))) continue;
                    int l = pezzo.gameObject.layer;
                    if (l != 0 && l != 8 && l != livello) continue; // 8: mobili segnati per sbaglio dalla prima versione
                    foreach (Transform t in pezzo.GetComponentsInChildren<Transform>(true))
                        if (t.gameObject.layer == 0 || t.gameObject.layer == 8) t.gameObject.layer = livello;
                    n++;
                }
            }
            return n;
        }

        const string ObstacleTraversalArredi = Valdorso.Movement.ObstacleTraversal.LivelloArredi;

        /// <summary>Se il livello 'numero' si chiama 'vecchio', gli dà il nome 'nuovo'.</summary>
        static void RinominaLivello(int numero, string vecchio, string nuovo)
        {
            Object[] asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0) return;
            var tm = new SerializedObject(asset[0]);
            SerializedProperty l = tm.FindProperty("layers").GetArrayElementAtIndex(numero);
            if (l.stringValue != vecchio) return;
            l.stringValue = nuovo;
            tm.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Valdorso] Livello {numero} rinominato \"{nuovo}\".");
        }

        /// <summary>Il numero del livello con quel nome; se manca, lo crea nel primo posto libero da 'primo' in poi.</summary>
        static int AssicuraLivello(string nome, int primo)
        {
            int esistente = LayerMask.NameToLayer(nome);
            if (esistente >= 0) return esistente;

            Object[] asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0) return -1;
            var tm = new SerializedObject(asset[0]);
            SerializedProperty livelli = tm.FindProperty("layers");
            for (int i = primo; i < livelli.arraySize; i++)
            {
                SerializedProperty l = livelli.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(l.stringValue)) continue;
                l.stringValue = nome;
                tm.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log($"[Valdorso] Creato il livello \"{nome}\" (numero {i}).");
                return i;
            }
            return -1;
        }
    }
}